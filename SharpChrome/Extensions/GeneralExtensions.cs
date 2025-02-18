﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SharpDPAPI;
using DateTime = System.DateTime;

namespace SharpChrome.Extensions
{
    public static class GeneralExtensions
    {
        /// <summary>
        /// Reads the current <see cref="Stream"/> as a <see cref="string"/>, optionally using a given <paramref name="encoding"/>.
        /// <para>Will also reset the Stream's internal position to 0 so it can be re-read again.</para>
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static async Task<string> ReadAsStringAsync(this Stream stream, Encoding encoding = null)
        {
            stream.Position = 0;
            var streamReader = encoding == null
                ? new StreamReader(stream, detectEncodingFromByteOrderMarks: true)
                : new StreamReader(stream, encoding);

            var @string = await streamReader.ReadToEndAsync();
            stream.Position = 0; // reset position
            return @string;
        }

        /// <summary>
        /// Reads the current <see cref="Stream"/> as a <see cref="string"/>, optionally using a given <paramref name="encoding"/>.
        /// <para>Will also reset the Stream's internal position to 0 so it can be re-read again.</para>
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static string ReadAsString(this Stream stream, Encoding encoding = null)
        {
            stream.Position = 0;
            var streamReader = encoding == null
                ? new StreamReader(stream, detectEncodingFromByteOrderMarks: true)
                : new StreamReader(stream, encoding);

            var @string = streamReader.ReadToEnd();
            stream.Position = 0; // reset position
            return @string;
        }

        /// <summary>
        /// Re-encrypt passwords using a given <paramref name="aesStateKey"/>.
        /// <para>Will encrypt the value for <see cref="logins.DecryptedPasswordValue"/> and update the <see cref="logins.password_value"/>.</para>
        /// <para>By default will throw an exception if a null value is encountered for <see cref="logins.DecryptedPasswordValue"/>.</para>
        /// </summary>
        /// <param name="logins"></param>
        /// <param name="aesStateKey"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="Exception"></exception>
        public static List<logins> ReEncryptPasswords(this IEnumerable<logins> logins, byte[] aesStateKey)
        {
            if (aesStateKey == null || aesStateKey.Length == default) throw new ArgumentNullException(nameof(aesStateKey));
            if (aesStateKey.Length != 32) throw new InvalidDataException($"{nameof(aesStateKey)} needs to be exactly 32 in length!");
            
            var keyDecryptResult = Chrome.DPAPIChromeAlgKeyFromRaw(aesStateKey, out var hAlg, out var hKey);
            if (false == keyDecryptResult) throw new Exception("Unable to determine algorithm key!");
            var loginsList = logins.ToList();

            foreach (var login in loginsList) {
                BinaryChromePass binaryChromePass = login.ToBinaryChromePass();
                login.password_value = null;
                
                byte[] decBytes = Encoding.ASCII.GetBytes(login.DecryptedPasswordValue);
                var reEncryptedPassword = Chrome.EncryptAESChromeBlob(decBytes, aesStateKey, binaryChromePass?.InitVector, binaryChromePass?.Tag);
                login.password_value = reEncryptedPassword;
            }

            return loginsList;
        }

        public static List<logins> DecryptPasswords(this IEnumerable<logins> logins, byte[] aesStateKey)
        {
            if (aesStateKey == null || aesStateKey.Length == default) throw new ArgumentNullException(nameof(aesStateKey));

            byte[] iv = new byte[12];
            BCrypt.SafeAlgorithmHandle hAlg = null;
            BCrypt.SafeKeyHandle hKey = null;

            var keyDecryptResult = Chrome.DPAPIChromeAlgKeyFromRaw(aesStateKey, out hAlg, out hKey);
            if (false == keyDecryptResult) throw new Exception("Unable to determine algorithm key!");
            var loginsList = logins.ToList();

            foreach (var login in loginsList) {
                byte[] passwordBytes = login.password_value;
                BinaryChromePass binaryChromePass = login.ToBinaryChromePass();
                byte[] decBytes = null;
                string password = null;

                if (HasV10Header(passwordBytes)) {
                    // using the new DPAPI decryption method
                    decBytes = Chrome.DecryptAESChromeBlob(passwordBytes, hAlg, hKey, out iv);

                    if (decBytes == null) {
                        throw new Exception($"Unable to decrypt {nameof(login.password_value)} ({login.username_value} :: {login.origin_url})");
                    }
                }
                else {
                    // using the old method
                    Dictionary<string, string> masterKeys = new Dictionary<string, string>();
                    decBytes = Dpapi.DescribeDPAPIBlob(blobBytes: passwordBytes, MasterKeys: masterKeys, blobType: "chrome");
                }
                
                password = Encoding.ASCII.GetString(decBytes);
                
                var reEncryptedTest = Chrome.EncryptAESChromeBlob(decBytes, aesStateKey, binaryChromePass);

                var encryptedOriginalStr = Helpers.ByteArrayToString(passwordBytes);
                var encryptedTestStr = Helpers.ByteArrayToString(reEncryptedTest);

                Debug.Assert(string.Equals(encryptedTestStr, encryptedOriginalStr, StringComparison.CurrentCultureIgnoreCase));

                var reDecrypted = Chrome.DecryptAESChromeBlob(reEncryptedTest, hAlg, hKey, out var _);

                var reDecryptedPasswordUt8 = Encoding.UTF8.GetString(reDecrypted);
                var reDecryptedPasswordAscii = Encoding.ASCII.GetString(reDecrypted);

                Debug.Assert(string.Equals(reDecryptedPasswordUt8, reDecryptedPasswordAscii, StringComparison.CurrentCultureIgnoreCase));
                Debug.Assert(string.Equals(reDecryptedPasswordAscii, password, StringComparison.CurrentCultureIgnoreCase));

                login.setDecrypted_password_value(password);
            }


            return loginsList;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasV10Header(this byte[] data) => Chrome.HasV10Header(data);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime ToDateTime(this double bigIntegerValue) => Helpers.ConvertToDateTime(bigIntegerValue);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToChromeTime(this DateTime dateTimeValue) => Helpers.ConvertToChromeTime(dateTimeValue);

        /// <summary>
        /// Concatenates the current array with the <paramref name="otherArray"/>, returning a new array.
        /// <para>The current array begins the new array.</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="otherArray"></param>
        /// <returns></returns>
        public static T[] ArrayConcat<T>(this T[] array, T[] otherArray)
        {
            T[] concatenated = new T[array.Length + otherArray.Length];
            array.CopyTo(concatenated, 0);
            otherArray.CopyTo(concatenated, array.Length);

            return concatenated;
        }

        /// <summary>
        /// Converts a <see cref="byte"/> pointer to a regular <see cref="byte"/> <see cref="Array"/>.
        /// </summary>
        /// <param name="bytePointer"></param>
        /// <param name="arrayLength"></param>
        /// <returns></returns>
        public static unsafe byte[] ToByteArray(byte* bytePointer, int arrayLength)
        {
            if (arrayLength <= 0) throw new ArgumentOutOfRangeException(nameof(arrayLength));

            var byteArray = new byte[arrayLength];
            Marshal.Copy((IntPtr)bytePointer, byteArray, 0, arrayLength);

            return byteArray;
        }

        public static BinaryChromePass ToSegmentedChromePass(this byte[] encryptedBytes)
        {
            var v10HeaderLength = 3;
            var header = encryptedBytes.Take(v10HeaderLength).ToArray();
            var initVector = encryptedBytes.Skip(v10HeaderLength).Take(Chrome.GCM_INITIALIZATION_VECTOR_SIZE).ToArray();

            const int tagLength = 16;
            var indexOfTagBegin = encryptedBytes.Length - tagLength;
            var password = encryptedBytes.Skip(v10HeaderLength + Chrome.GCM_INITIALIZATION_VECTOR_SIZE)
                .TakeWhile((b, i) => i < indexOfTagBegin)
                .ToArray();

            var tagShouldBeLast16 = encryptedBytes.TakeLast(tagLength).ToArray();
            
            return new BinaryChromePass() {
                Header = header,
                InitVector = initVector,
                Password = password,
                Tag = tagShouldBeLast16
            };
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int N)
        {
            return source.Skip(Math.Max(0, source.Count() - N));
        }

        public static string GetVendorName(this Browser browser)
        {
            var browserVendor = browser switch {
                Browser.BraveBrowser => "BraveSoftware",
                Browser.Chrome => "Google",
                Browser.Edge => "Microsoft",
                _ => throw new ArgumentOutOfRangeException(nameof(browser), browser, null)
            };

            return browserVendor;
        }

        public static string GetFullVendorAndBrowserName(this Browser browser)
        {
            return $"{browser.GetVendorName()} {browser.GetBrowserName()}";
        }

        public static string GetBrowserName(this Browser browser)
        {
            var browserName = browser switch {
                Browser.BraveBrowser => "Brave-Browser",
                _ => browser.ToString()
            };

            return browserName;
        }

        public static SharpDPAPI.Tuple<string, string> ResolveBrowserAndVendorNamesForUserDataPaths(this Browser browser)
        {
            var browserVendor = browser.GetVendorName();

            var browserName = browser.GetBrowserName();

            return new SharpDPAPI.Tuple<string, string>(browserVendor, browserName);
        }
        
        public static string ResolveBookmarksFilePath(this Browser browser, string directory)
        {
            var aesStateKeyPath = string.Format(Chrome.BookmarksPathFormattedTemplate, directory, browser.GetVendorName(), browser.GetBrowserName());
            if (!File.Exists(aesStateKeyPath)) {
                aesStateKeyPath = Path.Combine(directory, "Local State");
                if (!File.Exists(aesStateKeyPath)) {
                    throw new FileNotFoundException("Cant find browser book marks!", aesStateKeyPath);
                }
            }

            return aesStateKeyPath;
        }
        
        public static string ResolveLoginStatePath(this Browser browser, string directory)
        {
            var aesStateKeyPath = string.Format(Chrome.LocalStatePathFormattedTemplate, directory, browser.GetVendorName(), browser.GetBrowserName());
            if (!File.Exists(aesStateKeyPath)) {
                aesStateKeyPath = Path.Combine(directory, "Local State");
                if (!File.Exists(aesStateKeyPath)) {
                    throw new FileNotFoundException("Cant find browser master key!", aesStateKeyPath);
                }
            }

            return aesStateKeyPath;
        }

        public static string ResolveLoginDataPath(this Browser browser, string directory)
        {
            var loginDataPath = string.Format(Chrome.LoginDataPathFormattedTemplate, directory, browser.GetVendorName(), browser.GetBrowserName());
            if (!File.Exists(loginDataPath)) {
                loginDataPath = Path.Combine(directory, "Login Data");
                if (!File.Exists(loginDataPath)) {
                    throw new FileNotFoundException("Cant find login database!", loginDataPath);
                }
            }

            return loginDataPath;
        }

        public static void TryKillProcesses(this IEnumerable<Process> processes)
        {
            try {
                foreach (var p in processes) {
                    p.Kill();
                }
            }
            catch {
                return;
            }
        }

        /// <summary>
        /// Kill a process, and all of its children.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        private static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }

    }

    public class BinaryChromePass
    {
        public byte[] Header { get; set; }
        public byte[] InitVector { get; set; }
        public byte[] Password { get; set; }
        public byte[] Tag { get; set; }
    }
}