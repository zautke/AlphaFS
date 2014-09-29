﻿/* Copyright (c) 2008-2014 Peter Palotas, Alexandr Normuradov, Jeffrey Jangli
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 *  THE SOFTWARE.
 */

using Alphaleonis.Win32;
using Alphaleonis.Win32.Filesystem;
using Alphaleonis.Win32.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Directory = Alphaleonis.Win32.Filesystem.Directory;
using DirectoryInfo = Alphaleonis.Win32.Filesystem.DirectoryInfo;
using DriveInfo = Alphaleonis.Win32.Filesystem.DriveInfo;
using File = Alphaleonis.Win32.Filesystem.File;
using FileInfo = Alphaleonis.Win32.Filesystem.FileInfo;
using FileSystemInfo = Alphaleonis.Win32.Filesystem.FileSystemInfo;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace AlphaFS.UnitTest
{
   /// <summary>This is a test class for several AlphaFS instance classes.</summary>
   [TestClass]
   public class AlphaFS_ClassesTest
   {
      #region Unit Test Helpers

      #region Fields

      private readonly string LocalHost = Environment.MachineName;
      private readonly string LocalHostShare = Environment.SystemDirectory;
      private const string MyServer = "yomodo";
      private const string MyServerShare = @"\\" + MyServer + @"\video";
      private const string Local = @"LOCAL";
      private const string Network = @"NETWORK";

      private static readonly string SysDrive = Environment.GetEnvironmentVariable("SystemDrive");
      private static readonly string SysRoot = Environment.GetEnvironmentVariable("SystemRoot");
      private static readonly string SysRoot32 = Path.Combine(SysRoot, "System32");

      private const string TextTrue = "IsTrue";
      private const string TenNumbers = "0123456789";
      private const string TextHelloWorld = "Hëllõ Wørld!";
      private const string TextGoodByeWorld = "GóödByé Wôrld!";
      private const string TextAppend = "GóödByé Wôrld!";
      private const string TextUnicode = "ÛņïÇòdè; ǖŤƑ";

      #endregion // Fields

      #region CreateDirectoriesAndFiles

      private static void CreateDirectoriesAndFiles(string rootPath, int max, bool recurse)
      {
         for (int i = 0; i < max; i++)
         {
            string file = Path.Combine(rootPath, Path.GetRandomFileName());
            string dir = file + "-" + i + "-dir";
            file = file + "-" + i + "-file";

            Directory.CreateDirectory(dir);

            // Some directories will remain empty.
            if (i % 2 != 0)
            {
               File.WriteAllText(file, TextHelloWorld);
               File.WriteAllText(Path.Combine(dir, Path.GetFileName(file)), TextGoodByeWorld);
            }
         }

         if (recurse)
         {
            foreach (string dir in Directory.EnumerateDirectories(rootPath))
               CreateDirectoriesAndFiles(dir, max, false);
         }
      }

      #endregion // CreateDirectoriesAndFiles

      #region StopWatcher

      private static Stopwatch _stopWatcher;
      private static string StopWatcher(bool start = false)
      {
         if (_stopWatcher == null)
            _stopWatcher = new Stopwatch();

         if (start)
         {
            _stopWatcher.Restart();
            return null;
         }

         _stopWatcher.Stop();
         long ms = _stopWatcher.ElapsedMilliseconds;
         TimeSpan elapsed = _stopWatcher.Elapsed;

         return string.Format(CultureInfo.CurrentCulture, "*Duration: [{0}] ms. ({1})", ms, elapsed);
      }

      #endregion // StopWatcher

      #region Reporter

      private static string Reporter(bool onlyTime = false)
      {
         Win32Exception lastError = new Win32Exception();

         StopWatcher();

         return onlyTime
            ? string.Format(CultureInfo.CurrentCulture, "\t\t{0}", StopWatcher())
            : string.Format(CultureInfo.CurrentCulture, "\t{0} [{1}: {2}]", StopWatcher(), lastError.NativeErrorCode, lastError.Message);
      }

      #endregion // Reporter

      #region IsAdmin

      private static bool IsAdmin()
      {
         bool isAdmin = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

         if (!isAdmin)
            Console.WriteLine("\n\tThis Unit Test must be run as Administrator.");

         return isAdmin;
      }

      #endregion // IsAdmin

      #region Dump

      /// <summary>Shows the Object's available Properties and Values.</summary>
      private static bool Dump(object obj, int width = -35, bool indent = false)
      {
         int cnt = 0;
         const string nulll = "\t\tnull";
         string template = "\t{0}#{1:000}\t{2, " + width + "} == \t[{3}]";

         if (obj == null)
         {
            Console.WriteLine(nulll);
            return false;
         }

         Console.WriteLine("\n\t{0}Instance: [{1}]\n", indent ? "\t" : "", obj.GetType().FullName);

         bool loopOk = false;
         foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj).Sort().Cast<PropertyDescriptor>().Where(descriptor => descriptor != null))
         {
            string propValue;
            try
            {
               object value = descriptor.GetValue(obj);
               propValue = (value == null) ? "null" : value.ToString();

               loopOk = true;
            }
            catch (Exception ex)
            {
               // Please do tell, oneliner preferably.
               propValue = ex.Message.Replace(Environment.NewLine, string.Empty);
            }

            Console.WriteLine(template, indent ? "\t" : "", ++cnt, descriptor.Name, propValue);
         }

         return loopOk;
      }

      #endregion //Dump

      #region InputPaths

      static readonly string[] InputPaths =
      {
         @".",
         @".zip",
         
         Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture),
         Path.DirectorySeparatorChar + @"Program Files\Microsoft Office",
         
         Path.GlobalRootPrefix + @"device\harddisk0\partition1\",
         Path.VolumePrefix + @"{12345678-aac3-31de-3321-3124565341ed}\Program Files\notepad.exe",

         @"Program Files\Microsoft Office",
         @"C",
         @"C:",
         @"C:\",
         @"C:\a",
         @"C:\a\",
         @"C:\a\b",
         @"C:\a\b\",
         @"C:\a\b\c",
         @"C:\a\b\c\",
         @"C:\a\b\c\f",
         @"C:\a\b\c\f.",
         @"C:\a\b\c\f.t",
         @"C:\a\b\c\f.tx",
         @"C:\a\b\c\f.txt",

         Path.LongPathPrefix + @"Program Files\Microsoft Office",
         Path.LongPathPrefix + "C",
         Path.LongPathPrefix + @"C:",
         Path.LongPathPrefix + @"C:\",
         Path.LongPathPrefix + @"C:\a",
         Path.LongPathPrefix + @"C:\a\",
         Path.LongPathPrefix + @"C:\a\b",
         Path.LongPathPrefix + @"C:\a\b\",
         Path.LongPathPrefix + @"C:\a\b\c",
         Path.LongPathPrefix + @"C:\a\b\c\",
         Path.LongPathPrefix + @"C:\a\b\c\f",
         Path.LongPathPrefix + @"C:\a\b\c\f.",
         Path.LongPathPrefix + @"C:\a\b\c\f.t",
         Path.LongPathPrefix + @"C:\a\b\c\f.tx",
         Path.LongPathPrefix + @"C:\a\b\c\f.txt",

         Path.UncPrefix + @"Server\Share\",
         Path.UncPrefix + @"Server\Share\d",
         Path.UncPrefix + @"Server\Share\d1",
         Path.UncPrefix + @"Server\Share\d1\",
         Path.UncPrefix + @"Server\Share\d1\d",
         Path.UncPrefix + @"Server\Share\d1\d2",
         Path.UncPrefix + @"Server\Share\d1\d2\",
         Path.UncPrefix + @"Server\Share\d1\d2\f",
         Path.UncPrefix + @"Server\Share\d1\d2\fi",
         Path.UncPrefix + @"Server\Share\d1\d2\fil",
         Path.UncPrefix + @"Server\Share\d1\d2\file",
         Path.UncPrefix + @"Server\Share\d1\d2\file.",
         Path.UncPrefix + @"Server\Share\d1\d2\file.e",
         Path.UncPrefix + @"Server\Share\d1\d2\file.ex",
         Path.UncPrefix + @"Server\Share\d1\d2\file.ext",

         Path.LongPathUncPrefix + @"Server\Share\",
         Path.LongPathUncPrefix + @"Server\Share\d",
         Path.LongPathUncPrefix + @"Server\Share\d1",
         Path.LongPathUncPrefix + @"Server\Share\d1\",
         Path.LongPathUncPrefix + @"Server\Share\d1\d",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\f",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\fi",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\fil",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\file",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\file.",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\file.e",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\file.ex",
         Path.LongPathUncPrefix + @"Server\Share\d1\d2\file.ext"
      };

      #endregion // InputPaths

      #region StringToByteArray

      private static byte[] StringToByteArray(string str, params Encoding[] encoding)
      {
         Encoding encode = encoding != null && encoding.Any() ? encoding[0] : new UTF8Encoding(true, true);
         return encode.GetBytes(str);
      }

      #endregion // StringToByteArray

      #endregion // Unit Test Helpers

      #region Unit Tests

      #region Filesystem

      #region DumpClassBackupFileStream

      private void DumpClassBackupFileStream(bool isLocal)
      {
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);
         string tempPath = Path.GetTempPath("Class.BackupFileStream()-file-" + Path.GetRandomFileName());
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);
         Console.WriteLine("\nInput File Path: [{0}]", tempPath);

         string report;
         StopWatcher(true);
         using (BackupFileStream bfs = new BackupFileStream(tempPath, FileMode.Create))
         {
            report = Reporter();
            Dump(bfs, -10);
         }
         Console.WriteLine("\n{0}", report);

         File.Delete(tempPath, true);
         Assert.IsFalse(File.Exists(tempPath), "Cleanup failed: File should have been removed.");
         Console.WriteLine();
      }

      #endregion // DumpClassBackupFileStream

      #region DumpClassBackupStreamInfo

      private void DumpClassBackupStreamInfo(bool isLocal)
      {
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? "LOCAL" : "NETWORK");
         string tempPath = Path.GetTempPath("Class.BackupStreamInfo()-" + Path.GetRandomFileName());
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);
         Console.WriteLine("\nInput File Path: [{0}]", tempPath);

         string report;
         using (SafeFileHandle handle = File.Create(tempPath).SafeFileHandle)
         {
            StopWatcher(true);
            BackupStreamInfo bsi = new BackupStreamInfo(new Alphaleonis.Win32.Filesystem.NativeMethods.Win32StreamId(), "test", handle);
            report = Reporter(true);

            Dump(bsi, -10);

            Assert.IsFalse(handle == null, "Handle should not be null.");
            Assert.IsFalse(handle.IsClosed, "Handle should not be closed.");
            Assert.IsFalse(handle.IsInvalid, "Hanlde should not be invalid.");
         }
         Console.WriteLine("\n\t{0}", report);

         File.Delete(tempPath, true);
         Assert.IsFalse(File.Exists(tempPath), "Cleanup failed: File should have been removed.");
         Console.WriteLine("\n");
      }

      #endregion // DumpClassBackupStreamInfo

      #region DumpClassByHandleFileInformation

      [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
      private void DumpClassByHandleFileInformation(bool isLocal)
      {
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);
         string tempPath = Path.GetTempPath("File-Class.ByHandleFileInformation()-" + Path.GetRandomFileName());
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         Console.WriteLine("\nInput File Path: [{0}]", tempPath);

         using (FileStream stream = File.Create(tempPath))
         {
            stream.WriteByte(1);

            string streamName = stream.Name;

            StopWatcher(true);
            ByHandleFileInformation info = File.GetFileInformationByHandle(stream.SafeFileHandle);
            Console.WriteLine("\n\tFilestream.Name == [{0}]\n{1}", streamName, Reporter());
            Assert.IsTrue(Dump(info, -18));

            stream.Dispose();

            Assert.AreEqual(System.IO.File.GetCreationTime(tempPath), info.CreationTime);
            Assert.AreEqual(System.IO.File.GetLastAccessTime(tempPath), info.LastAccessTime);
            Assert.AreEqual(System.IO.File.GetLastWriteTime(tempPath).ToString(), info.LastWriteTime.ToString()); // Curious

            File.Delete(tempPath, true);
            Assert.IsFalse(File.Exists(tempPath), "Cleanup failed: File should have been removed.");
            Console.WriteLine();
         }
      }

      #endregion // DumpClassByHandleFileInformation

      #region DumpClassDeviceInfo

      private void DumpClassDeviceInfo(string host)
      {
         Console.WriteLine("\n=== TEST ===");
         string tempPath = host; 

         bool allOk = false;
         try
         {
            #region DeviceGuid.Volume

            Console.Write("\nEnumerating volumes from host: [{0}]\n", tempPath);
            int cnt = 0;
            StopWatcher(true);
            foreach (DeviceInfo device in Device.EnumerateDevices(tempPath, DeviceGuid.Volume))
            {
               Console.WriteLine("\n#{0:000}\tClass: [{1}]", ++cnt, device.Class);

               Dump(device, -24);

               try
               {
                  string getDriveLetter = Volume.GetDriveNameForNtDeviceName(device.PhysicalDeviceObjectName);
                  string dosdeviceGuid = Volume.GetVolumeGuidForNtDeviceName(device.PhysicalDeviceObjectName);

                  Console.WriteLine("\n\tVolume.GetDriveNameForNtDeviceName() : [{0}]", getDriveLetter ?? "null");
                  Console.WriteLine("\tVolume.GetVolumeGuidForNtDeviceName(): [{0}]", dosdeviceGuid ?? "null");
               }
               catch (Exception ex)
               {
                  Console.WriteLine("\nCaught Exception (0): [{0}]", ex.Message.Replace(Environment.NewLine, string.Empty));
               }
            }
            Console.WriteLine("\n{0}", Reporter());
            Console.WriteLine();

            #endregion // DeviceGuid.Volume

            #region DeviceGuid.Disk

            Console.Write("\nEnumerating volumes from host: [{0}]\n", tempPath);
            cnt = 0;
            StopWatcher(true);
            foreach (DeviceInfo device in Device.EnumerateDevices(tempPath, DeviceGuid.Disk))
            {
               Console.WriteLine("\n#{0:000}\tClass: [{1}]", ++cnt, device.Class);

               Dump(device, -24);

               try
               {
                  string getDriveLetter = Volume.GetDriveNameForNtDeviceName(device.PhysicalDeviceObjectName);
                  string dosdeviceGuid = Volume.GetVolumeGuidForNtDeviceName(device.PhysicalDeviceObjectName);

                  Console.WriteLine("\n\tVolume.GetDriveNameForNtDeviceName() : [{0}]", getDriveLetter ?? "null");
                  Console.WriteLine("\tVolume.GetVolumeGuidForNtDeviceName(): [{0}]", dosdeviceGuid ?? "null");
               }
               catch (Exception ex)
               {
                  Console.WriteLine("\nCaught Exception (0): [{0}]", ex.Message.Replace(Environment.NewLine, string.Empty));
               }
            }
            Console.WriteLine("\n{0}\n", Reporter());

            #endregion // DeviceGuid.Disk

            allOk = true;
         }
         catch (Exception ex)
         {
            Console.WriteLine("\tCaught Exception (1): [{0}]", ex.Message.Replace(Environment.NewLine, string.Empty));
         }

         Assert.IsTrue(allOk, "Could (probably) not connect to host: [{0}]", host);
         Console.WriteLine();
      }

      #endregion // DumpClassDeviceInfo

      #region DumpClassDirectoryInfo
      private static void CompareDirectoryInfos(System.IO.DirectoryInfo expected, DirectoryInfo actual)
      {
         Dump(expected, -17);
         Dump(actual, -17);

         int errorCnt = 0;
         int cnt = -1;
         while (cnt != 13)
         {
            cnt++;

            if (expected == null || actual == null)
               Assert.AreEqual(expected, actual, "One or both of the DirectoryInfo instances is/are null.");

            else
            {
               try
               {
                  // Compare values of both instances.
                  switch (cnt)
                  {
                     case 0: Assert.AreEqual(expected.Attributes, actual.Attributes, "Attributes AlphaFS != System.IO"); break;
                     case 1: Assert.AreEqual(expected.CreationTime, actual.CreationTime, "CreationTime AlphaFS != System.IO"); break;
                     case 2: Assert.AreEqual(expected.CreationTimeUtc, actual.CreationTimeUtc, "CreationTimeUtc AlphaFS != System.IO"); break;
                     case 3: Assert.AreEqual(expected.Exists, actual.Exists, "Exists AlphaFS != System.IO"); break;
                     case 4: Assert.AreEqual(expected.Extension, actual.Extension, "Extension AlphaFS != System.IO"); break;
                     case 5: Assert.AreEqual(expected.FullName, actual.FullName, "FullName AlphaFS != System.IO"); break;
                     case 6: Assert.AreEqual(expected.LastAccessTime, actual.LastAccessTime, "LastAccessTime AlphaFS != System.IO"); break;
                     case 7: Assert.AreEqual(expected.LastAccessTimeUtc, actual.LastAccessTimeUtc, "LastAccessTimeUtc AlphaFS != System.IO"); break;
                     case 8: Assert.AreEqual(expected.LastWriteTime, actual.LastWriteTime, "LastWriteTime AlphaFS != System.IO"); break;
                     case 9: Assert.AreEqual(expected.LastWriteTimeUtc, actual.LastWriteTimeUtc, "LastWriteTimeUtc AlphaFS != System.IO"); break;
                     case 10: Assert.AreEqual(expected.Name, actual.Name, "Name AlphaFS != System.IO"); break;

                     // Need .ToString() here since the object types are obviously not the same.
                     case 11: Assert.AreEqual(expected.Parent.ToString(), actual.Parent.ToString(), "Parent AlphaFS != System.IO"); break;
                     case 12: Assert.AreEqual(expected.Root.ToString(), actual.Root.ToString(), "Root AlphaFS != System.IO"); break;
                  }
               }
               catch (Exception ex)
               {
                  errorCnt++;
                  Console.WriteLine("\n\t\tProperty cnt #{0}\tCaught Exception: [{1}]", (cnt + 1), ex.Message.Replace(Environment.NewLine, string.Empty));
               }
            }
         }

         //Assert.IsTrue(errorCnt == 0, "\tEncountered: [{0}] DirectoryInfo Properties where AlphaFS != System.IO", errorCnt);
         Console.WriteLine();
      }

      private static void DumpClassDirectoryInfo(bool isLocal)
      {
         #region Current Directory: .
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);
         string tempPath = Path.CurrentDirectoryPrefix;
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         Console.WriteLine("\nInput Directory Path: [{0}] (dot = Current directory)\n", tempPath);

         StopWatcher(true);
         System.IO.DirectoryInfo expected = new System.IO.DirectoryInfo(tempPath);
         Console.WriteLine("\tSystem.IO DirectoryInfo(){0}", Reporter());

         StopWatcher(true);
         DirectoryInfo actual = new DirectoryInfo(tempPath);
         Console.WriteLine("\tAlphaFS DirectoryInfo(){0}", Reporter());

         // Compare values of both instances.
         CompareDirectoryInfos(expected, actual);
         #endregion // Current Directory: .

         #region Non-Existing Directory
         tempPath = string.Format(@"{0}:\Non-Existing-Directory", DriveInfo.GetFreeDriveLetter());
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         Console.WriteLine("\nInput Directory Path: [{0}]\n", tempPath);

         StopWatcher(true);
         expected = new System.IO.DirectoryInfo(tempPath);
         Console.WriteLine("\tSystem.IO DirectoryInfo(){0}", Reporter());

         StopWatcher(true);
         actual = new DirectoryInfo(tempPath);
         Console.WriteLine("\tAlphaFS DirectoryInfo(){0}", Reporter());

         // Compare values of both instances.
         CompareDirectoryInfos(expected, actual);
         #endregion // Non-Existing Directory

         #region Existing Directory
         tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         Directory.CreateDirectory(tempPath);

         Console.WriteLine("\nInput Directory Path: [{0}]\n", tempPath);

         StopWatcher(true);
         expected = new System.IO.DirectoryInfo(tempPath);
         Console.WriteLine("\tSystem.IO DirectoryInfo(){0}", Reporter());

         StopWatcher(true);
         actual = new DirectoryInfo(tempPath);
         Console.WriteLine("\tAlphaFS DirectoryInfo(){0}", Reporter());

         // Compare values of both instances.
         CompareDirectoryInfos(expected, actual);

         Directory.Delete(tempPath);
         Assert.IsFalse(Directory.Exists(tempPath), "Cleanup failed: Directory should have been removed.");
         #endregion // Existing Directory

         #region Method .ToString() (Issue-21118)
         Console.WriteLine("\nMethod .ToString()");

         DirectoryInfo dirAlphaFs = new DirectoryInfo("ToString()-TestFolder");
         System.IO.DirectoryInfo dirSysIo = new System.IO.DirectoryInfo("ToString()-TestFolder");

         string dirAlphaFsToString = dirAlphaFs.ToString();
         string dirSysIoToString = dirSysIo.ToString();

         Console.WriteLine("\tBoth strings should be the same.");
         Console.WriteLine("\tAlphaFS  : [{0}]", dirAlphaFsToString);
         Console.WriteLine("\tSystem.IO: [{0}]", dirSysIoToString);

         Assert.AreEqual(dirSysIoToString, dirAlphaFsToString, false);
         Console.WriteLine();

         #endregion Method .ToString()
      }

      #endregion // DumpClassDirectoryInfo

      #region DumpClassDiskSpaceInfo

      private void DumpClassDiskSpaceInfo(bool isLocal)
      {
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);
         string tempPath = SysDrive;
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);
         Console.WriteLine("\nInput Path: [{0}]", tempPath);

         StopWatcher(true);
         DiskSpaceInfo dsi = new DiskSpaceInfo(tempPath, null, true, false);
         string report = Reporter();

         Assert.IsTrue(Dump(dsi, -26));
         Assert.IsTrue(dsi.BytesPerSector != 0 && dsi.NumberOfFreeClusters != 0 && dsi.SectorsPerCluster != 0 && dsi.TotalNumberOfClusters != 0);
         Assert.IsTrue(dsi.FreeBytesAvailable != 0 && dsi.TotalNumberOfBytes != 0 && dsi.TotalNumberOfFreeBytes != 0);

         Console.WriteLine("\n{0}", report);
         Console.WriteLine();
      }

      #endregion // DumpClassDiskSpaceInfo

      #region DumpClassDriveInfo

      private static void DumpClassDriveInfo(bool isLocal, string drive)
      {
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);
         string tempPath = drive;
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         StopWatcher(true);
         DriveInfo actual = new DriveInfo(tempPath);
         Console.WriteLine("\nInput Path: [{0}]{1}", tempPath, Reporter());

         #region Local Drive
         if (isLocal)
         {
            // System.IO.DriveInfo() can not handle UNC paths.
            System.IO.DriveInfo expected = new System.IO.DriveInfo(tempPath);
            //Dump(expected, -21);

            // Even 1 byte more or less results in failure, so do these tests asap.
            Assert.AreEqual(expected.AvailableFreeSpace, actual.AvailableFreeSpace, "AvailableFreeSpace AlphaFS != System.IO");
            Assert.AreEqual(expected.TotalFreeSpace, actual.TotalFreeSpace, "TotalFreeSpace AlphaFS != System.IO");
            Assert.AreEqual(expected.TotalSize, actual.TotalSize, "TotalSize AlphaFS != System.IO");

            Assert.AreEqual(expected.DriveFormat, actual.DriveFormat, "DriveFormat AlphaFS != System.IO");
            Assert.AreEqual(expected.DriveType, actual.DriveType, "DriveType AlphaFS != System.IO");
            Assert.AreEqual(expected.IsReady, actual.IsReady, "IsReady AlphaFS != System.IO");
            Assert.AreEqual(expected.Name, actual.Name, "Name AlphaFS != System.IO");
            Assert.AreEqual(expected.RootDirectory.ToString(), actual.RootDirectory.ToString(), "RootDirectory AlphaFS != System.IO");
            Assert.AreEqual(expected.VolumeLabel, actual.VolumeLabel, "VolumeLabel AlphaFS != System.IO");
         }

         #region Class Equality

         ////int getHashCode1 = actual.GetHashCode();

         //DriveInfo driveInfo2 = new DriveInfo(tempPath);
         ////int getHashCode2 = driveInfo2.GetHashCode();

         //DriveInfo clone = actual;
         ////int getHashCode3 = clone.GetHashCode();

         //bool isTrue1 = clone.Equals(actual);
         //bool isTrue2 = clone == actual;
         //bool isTrue3 = !(clone != actual);
         //bool isTrue4 = actual == driveInfo2;
         //bool isTrue5 = !(actual != driveInfo2);

         ////Console.WriteLine("\n\t\t actual.GetHashCode() : [{0}]", getHashCode1);
         ////Console.WriteLine("\t\t     clone.GetHashCode() : [{0}]", getHashCode3);
         ////Console.WriteLine("\t\tdriveInfo2.GetHashCode() : [{0}]\n", getHashCode2);

         ////Console.WriteLine("\t\t obj clone.ToString() == [{0}]", clone.ToString());
         ////Console.WriteLine("\t\t obj clone.Equals()   == [{0}] : {1}", TextTrue, isTrue1);
         ////Console.WriteLine("\t\t obj clone ==         == [{0}] : {1}", TextTrue, isTrue2);
         ////Console.WriteLine("\t\t obj clone !=         == [{0}]: {1}\n", TextFalse, isTrue3);

         ////Console.WriteLine("\t\tdriveInfo == driveInfo2 == [{0}] : {1}", TextTrue, isTrue4);
         ////Console.WriteLine("\t\tdriveInfo != driveInfo2 == [{0}] : {1}", TextFalse, isTrue5);

         //Assert.IsTrue(isTrue1, "clone.Equals(actual)");
         //Assert.IsTrue(isTrue2, "clone == actual");
         //Assert.IsTrue(isTrue3, "!(clone != actual)");
         //Assert.IsTrue(isTrue4, "actual == driveInfo2");
         //Assert.IsTrue(isTrue5, "!(actual != driveInfo2)");

         #endregion // Class Equality

         #endregion // Local Drive

         StopWatcher(true);
         Dump(actual, -21);

         Dump(actual.DiskSpaceInfo, -26);

         //if (expected != null) Dump(expected.RootDirectory, -17);
         //Dump(actual.RootDirectory, -17);

         Dump(actual.VolumeInfo, -26);

         Console.WriteLine("\n{0}", Reporter());
         Console.WriteLine();
      }

      #endregion // DumpClassDriveInfo

      #region DumpClassFileInfo

      private static void CompareFileInfos(System.IO.FileInfo expected, FileInfo actual)
      {
         if (expected == null || actual == null)
            Assert.AreEqual(expected, actual, "Mismatch");

         Dump(expected, -17);
         Dump(actual, -17);

         int errorCnt = 0;
         int cnt = -1;
         while (cnt != 15)
         {
            cnt++;
            try
            {
               // Compare values of both instances.
               switch (cnt)
               {
                  case 0:
                     Assert.AreEqual(expected.Attributes, actual.Attributes, "Attributes AlphaFS != System.IO");
                     break;
                  case 1:
                     Assert.AreEqual(expected.CreationTime, actual.CreationTime, "CreationTime AlphaFS != System.IO");
                     break;
                  case 2:
                     Assert.AreEqual(expected.CreationTimeUtc, actual.CreationTimeUtc,
                        "CreationTimeUtc AlphaFS != System.IO");
                     break;

                     // Need .ToString() here since the object types are obviously not the same.
                  case 3:
                     Assert.AreEqual(expected.Directory.ToString(), actual.Directory.ToString(),
                        "Directory AlphaFS != System.IO");
                     break;

                  case 4:
                     Assert.AreEqual(expected.DirectoryName, actual.DirectoryName, "DirectoryName AlphaFS != System.IO");
                     break;
                  case 5:
                     Assert.AreEqual(expected.Exists, actual.Exists, "Exists AlphaFS != System.IO");
                     break;
                  case 6:
                     Assert.AreEqual(expected.Extension, actual.Extension, "Extension AlphaFS != System.IO");
                     break;
                  case 7:
                     Assert.AreEqual(expected.FullName, actual.FullName, "FullName AlphaFS != System.IO");
                     break;
                  case 8:
                     Assert.AreEqual(expected.IsReadOnly, actual.IsReadOnly, "IsReadOnly AlphaFS != System.IO");
                     break;
                  case 9:
                     Assert.AreEqual(expected.LastAccessTime, actual.LastAccessTime,
                        "LastAccessTime AlphaFS != System.IO");
                     break;
                  case 10:
                     Assert.AreEqual(expected.LastAccessTimeUtc, actual.LastAccessTimeUtc,
                        "LastAccessTimeUtc AlphaFS != System.IO");
                     break;
                  case 11:
                     Assert.AreEqual(expected.LastWriteTime, actual.LastWriteTime, "LastWriteTime AlphaFS != System.IO");
                     break;
                  case 12:
                     Assert.AreEqual(expected.LastWriteTimeUtc, actual.LastWriteTimeUtc,
                        "LastWriteTimeUtc AlphaFS != System.IO");
                     break;
                  case 13:
                     Assert.AreEqual(expected.Length, actual.Length, "Length AlphaFS != System.IO");
                     break;
                  case 14:
                     Assert.AreEqual(expected.Name, actual.Name, "Name AlphaFS != System.IO");
                     break;
               }
            }
            catch (Exception ex)
            {
               errorCnt++;
               Console.WriteLine("\n\t\t\tProperty cnt #{0}\tCaught Exception: [{1}]", (cnt + 1), ex.Message.Replace(Environment.NewLine, string.Empty));
            }
         }

         //Assert.IsTrue(errorCnt == 0, "\tEncountered: [{0}] FileInfo Properties where AlphaFS != System.IO", errorCnt);
         Console.WriteLine();
      }

      private static void DumpClassFileInfo(bool isLocal)
      {
         #region Current Directory: .

         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);
         string tempPath = Path.CurrentDirectoryPrefix;
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         Console.WriteLine("\nInput File Path: [{0}] (dot = Current directory)\n", tempPath);

         StopWatcher(true);
         System.IO.FileInfo expected = new System.IO.FileInfo(tempPath);
         Console.WriteLine("\tSystem.IO FileInfo(){0}", Reporter());

         StopWatcher(true);
         FileInfo actual = new FileInfo(tempPath);
         Console.WriteLine("\tAlphaFS FileInfo(){0}", Reporter());

         // Compare values of both instances.
         CompareFileInfos(expected, actual);

         #endregion // Current Directory: .

         #region Non-Existing File

         tempPath = string.Format(@"{0}:\Non-Existing-File.txt", DriveInfo.GetFreeDriveLetter());
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         Console.WriteLine("\nInput File Path: [{0}]\n", tempPath);

         StopWatcher(true);
         expected = new System.IO.FileInfo(tempPath);
         Console.WriteLine("\tSystem.IO FileInfo(){0}", Reporter());

         StopWatcher(true);
         actual = new FileInfo(tempPath);
         Console.WriteLine("\tAlphaFS FileInfo(){0}", Reporter());


         // Compare values of both instances.
         CompareFileInfos(expected, actual);

         #endregion // Non-Existing File

         #region Existing File

         tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         using (File.Create(tempPath))
         {
         }

         Console.WriteLine("\nInput File Path: [{0}]\n", tempPath);

         StopWatcher(true);
         expected = new System.IO.FileInfo(tempPath);
         Console.WriteLine("\tSystem.IO FileInfo(){0}", Reporter());

         StopWatcher(true);
         actual = new FileInfo(tempPath);
         Console.WriteLine("\tAlphaFS FileInfo(){0}", Reporter());


         // Compare values of both instances.
         CompareFileInfos(expected, actual);

         File.Delete(tempPath);
         Assert.IsFalse(File.Exists(tempPath), "Cleanup failed: File should have been removed.");

         #endregion // Existing File

         #region Method .ToString() (Issue: 21118)

         Console.WriteLine("\nMethod .ToString()");

         FileInfo fileAlphaFs = new FileInfo("ToString()-TestFile");
         System.IO.FileInfo fileSysIo = new System.IO.FileInfo("ToString()-TestFile");

         string fileAlphaFsToString = fileAlphaFs.ToString();
         string fileSysIoToString = fileSysIo.ToString();

         Console.WriteLine("\tBoth strings should be the same.");
         Console.WriteLine("\tAlphaFS  : [{0}]", fileAlphaFsToString);
         Console.WriteLine("\tSystem.IO: [{0}]", fileSysIoToString);

         Assert.AreEqual(fileSysIoToString, fileAlphaFsToString, false);
         Console.WriteLine();

         #endregion Method .ToString()
      }

      #endregion // DumpClassFileInfo

      #region DumpClassFileSystemEntryInfo

      private void DumpClassFileSystemEntryInfo(bool isLocal)
      {
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);

         #region Directory

         string path = SysRoot;
         if (!isLocal) path = Path.LocalToUnc(path);

         Console.WriteLine("\nInput Directory Path: [{0}]", path);

         StopWatcher(true);
         FileSystemEntryInfo fsei = File.GetFileSystemEntryInfo(path);
         Console.WriteLine(Reporter());
         
         Assert.IsFalse(fsei == null);
         Assert.IsTrue(Dump(fsei, -17));

         #endregion // Directory

         #region File

         path = Path.Combine(SysRoot, "notepad.exe");
         if (!isLocal) path = Path.LocalToUnc(path);

         Console.WriteLine("\nInput File Path: [{0}]", path);
         
         StopWatcher(true);
         fsei = File.GetFileSystemEntryInfo(path);
         Console.WriteLine(Reporter());


         Assert.IsFalse(fsei == null);
         Assert.IsTrue(Dump(fsei, -17));
         #endregion // File

         Console.WriteLine();
      }

      #endregion // DumpClassFileSystemEntryInfo

      #region DumpClassShell32Info

      private void DumpClassShell32Info(bool isLocal)
      {
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);
         string tempPath = Path.GetTempPath("Class.Shell32Info()-" + Path.GetRandomFileName());
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);

         Console.WriteLine("\nInput File Path: [{0}]\n", tempPath);

         using (File.Create(tempPath))
         {}


         
         StopWatcher(true);
         Shell32Info shell32Info = Shell32.GetShell32Information(tempPath);
         string report = Reporter();

         Console.WriteLine("\tMethod: Shell32Info.Refresh()");
         Console.WriteLine("\tMethod: Shell32Info.GetIcon()");
         

         string cmd = "print";
         Console.WriteLine("\tMethod: Shell32Info.GetVerbCommand(\"{0}\") == [{1}]", cmd, shell32Info.GetVerbCommand(cmd));

         Assert.IsTrue(Dump(shell32Info, -15));

         File.Delete(tempPath, true);
         Assert.IsFalse(File.Exists(tempPath), "Cleanup failed: File should have been removed.");

         Console.WriteLine("\n{0}", report);
         Console.WriteLine();
      }

      #endregion // DumpClassShell32Info

      #region DumpClassVolumeInfo

      private void DumpClassVolumeInfo(bool isLocal)
      {
         Console.WriteLine("\n=== TEST {0} ===", isLocal ? Local : Network);
         string tempPath = SysDrive;
         if (!isLocal) tempPath = Path.LocalToUnc(tempPath);
         Console.WriteLine("\nInput Directory Path: [{0}]", tempPath);

         StopWatcher(true);
         VolumeInfo volInfo = Volume.GetVolumeInformation(tempPath);
         string report = Reporter();
         Assert.IsTrue(Dump(volInfo, -26));
         Console.WriteLine("\n{0}", report);
         Console.WriteLine();
      }

      #endregion // DumpClassVolumeInfo

      #endregion // Filesystem

      #region Network

      #region DumpClassDfsInfo

      private void DumpClassDfsInfo()
      {
         int cnt = 0;
         StopWatcher(true);
         try
         {
            foreach (string dfsNamespace in Host.EnumerateDomainDfsRoot())
            {
               try
               {
                  Console.Write("\n#{0:000}\tDFS Root: [{1}]\n", ++cnt, dfsNamespace);

                  DfsInfo dfsInfo = Host.GetDfsInformation(dfsNamespace);

                  Console.Write("\nDirectory contents:\tSubdirectories: [{0}]\tFiles: [{1}]\n",
                     dfsInfo.DirectoryInfo.CountDirectories(true), dfsInfo.DirectoryInfo.CountFiles(true));

                  Dump(dfsInfo, -16);

                  Console.Write("\n\tNumber of Storages: [{0}]\n", dfsInfo.NumberOfStorages.Count());

                  foreach (DfsStorage store in dfsInfo.NumberOfStorages)
                  {
                     Dump(store, -10);

                     // DFS shares (non SMB) cannot be retrieved.
                     ShareInfo share = Host.GetShareInformation(1005, store.ServerName, store.ShareName, true);
                     Dump(share, -18);
                     Console.Write("\n");
                  }
               }
               catch (Exception ex)
               {
                  Console.Write("\nCaught Exception: [{0}]\n\n", ex.Message.Replace(Environment.NewLine, string.Empty));
               }
            }
            Console.Write("\n{0}", Reporter());
            Assert.IsTrue(cnt > 0, "Nothing was enumerated.");
         }
         catch (Exception ex)
         {
            Console.Write("\nCaught Exception (1): [{0}]", ex.Message.Replace(Environment.NewLine, string.Empty));
         }

         Console.WriteLine("\n\n\t{0}", Reporter(true));
         Assert.IsTrue(cnt > 0, "Nothing was enumerated.");

         Console.WriteLine();
      }

      #endregion // DumpClassDfsInfo

      #region DumpOpenConnectionInfo

      private void DumpOpenConnectionInfo(string host)
      {
         Console.WriteLine("\n=== TEST ===");
         Console.WriteLine("\nNetwork.Host.EnumerateOpenResources() from host: [{0}]", host);

         StopWatcher(true);
         foreach (OpenConnectionInfo connectionInfo in Host.EnumerateOpenConnections(host, "IPC$", false))
            Dump(connectionInfo, -17);

         Console.WriteLine("\n{0}", Reporter());
         Console.WriteLine();
      }
      
      #endregion // DumpClassOpenResourceInfo

      #region DumpClassOpenResourceInfo

      private void DumpClassOpenResourceInfo(string host, string share)
      {
         Console.WriteLine("\n=== TEST ===");
         string tempPath = Path.LocalToUnc(share);
         Console.WriteLine("\nNetwork.Host.EnumerateOpenResources() from host: [{0}]", tempPath);

         Directory.SetCurrentDirectory(tempPath);

         StopWatcher(true);
         int cnt = 0;
         foreach (OpenResourceInfo openResource in Host.EnumerateOpenResources(host, null, null, false))
         {
            if (Dump(openResource, -11))
            {
               Console.Write("\n");
               cnt++;
            }
         }

         Console.WriteLine(Reporter());
         Assert.IsTrue(cnt > 0, "Nothing was enumerated.");
         Console.WriteLine();
      }
      
      #endregion // DumpClassOpenResourceInfo

      #region DumpClassShareInfo

      private void DumpClassShareInfo(string host)
      {
         Console.WriteLine("\n=== TEST ===");
         Console.Write("\nNetwork.Host.EnumerateShares() from host: [{0}]\n", host);

         int cnt = 0;
         StopWatcher(true);
         foreach (ShareInfo share in Host.EnumerateShares(host, true))
         {
            Console.WriteLine("\n\t#{0:000}\tShare: [{1}]", ++cnt, share);
            Dump(share, -18);
            Console.Write("\n");
         }

         Console.WriteLine(Reporter());
         Assert.IsTrue(cnt > 0, "Nothing was enumerated.");
         Console.WriteLine();
      }

      #endregion // DumpClassShareInfo

      #endregion // Network

      #endregion // Unit Tests

      #region Unit Test Callers

      #region Filesystem

      #region Class_Filesystem_BackupFileStream

      [TestMethod]
      public void Class_Filesystem_BackupFileStream()
      {
         Console.WriteLine("Class Filesystem.BackupFileStream()");

         DumpClassBackupFileStream(true);
         DumpClassBackupFileStream(false);
      }

      #endregion // Class_Filesystem_BackupFileStream

      #region Class_Filesystem_BackupStreamInfo

      [TestMethod]
      public void Class_Filesystem_BackupStreamInfo()
      {
         Console.WriteLine("Class Filesystem.BackupStreamInfo()");

         DumpClassBackupStreamInfo(true);
         DumpClassBackupStreamInfo(false);
      }

      #endregion // Class_Filesystem_BackupStreamInfo

      #region Class_Filesystem_ByHandleFileInformation

      [TestMethod]
      public void Class_Filesystem_ByHandleFileInformation()
      {
         Console.WriteLine("Class Filesystem.ByHandleFileInformation()");

         DumpClassByHandleFileInformation(true);
         DumpClassByHandleFileInformation(false);
      }

      #endregion // Class_Filesystem_ByHandleFileInformation

      #region Class_Filesystem_DeviceInfo

      [TestMethod]
      public void Class_Filesystem_DeviceInfo()
      {
         Console.WriteLine("Class Filesystem.DeviceInfo()");
         Console.WriteLine("\nMSDN Note: Beginning in Windows 8 and Windows Server 2012 functionality to access remote machines has been removed.");
         Console.WriteLine("You cannot access remote machines when running on these versions of Windows.\n");

         DumpClassDeviceInfo(LocalHost);
         //DumpClassDeviceInfo(MyServer);
      }
      
      #endregion // Class_Filesystem_Shell32Info

      #region Class_Filesystem_DirectoryInfo

      [TestMethod]
      public void Class_Filesystem_DirectoryInfo()
      {
         Console.WriteLine("Class Filesystem.DirectoryInfo()");

         DumpClassDirectoryInfo(true);
         DumpClassDirectoryInfo(false);
      }

      #endregion // Class_Filesystem_DirectoryInfo

      #region Class_Filesystem_DiskSpaceInfo

      [TestMethod]
      public void Class_Filesystem_DiskSpaceInfo()
      {
         Console.WriteLine("Class Filesystem.DiskSpaceInfo()");

         DumpClassDiskSpaceInfo(true);
         DumpClassDiskSpaceInfo(false);
      }

      #endregion // Class_Filesystem_DiskSpaceInfo

      #region Class_Filesystem_DriveInfo

      [TestMethod]
      public void Class_Filesystem_DriveInfo()
      {
         Console.WriteLine("Class Filesystem.DriveInfo()");

         DumpClassDriveInfo(true, SysDrive);
         DumpClassDriveInfo(false, SysDrive);
      }

      #endregion // Class_Filesystem_DriveInfo

      #region Class_Filesystem_FileInfo

      [TestMethod]
      public void Class_Filesystem_FileInfo()
      {
         Console.WriteLine("Class Filesystem.FileInfo()");

         DumpClassFileInfo(true);
         DumpClassFileInfo(false);
      }

      #endregion // Class_Filesystem_FileInfo

      #region Class_Filesystem_FileSystemEntryInfo

      [TestMethod]
      public void Class_Filesystem_FileSystemEntryInfo()
      {
         Console.WriteLine("Class Filesystem.FileSystemEntryInfo()");

         DumpClassFileSystemEntryInfo(true);
         DumpClassFileSystemEntryInfo(false);
      }

      #endregion // Class_Filesystem_FileSystemEntryInfo

      #region Class_Filesystem_Shell32Info

      [TestMethod]
      public void Class_Filesystem_Shell32Info()
      {
         Console.WriteLine("Class Filesystem.Shell32Info()");

         DumpClassShell32Info(true);
         DumpClassShell32Info(false);
      }

      #endregion // Class_Filesystem_Shell32Info

      #region Class_Filesystem_VolumeInfo

      [TestMethod]
      public void Class_Filesystem_VolumeInfo()
      {
         Console.WriteLine("Class Filesystem.VolumeInfo()");

         DumpClassVolumeInfo(true);
         DumpClassVolumeInfo(false);
      }

      #endregion // Class_Filesystem_VolumeInfo

      #endregion Filesystem
      
      #region Network

      #region Class_Network_DfsInfo

      [TestMethod]
      public void Class_Network_DfsInfo()
      {
         Console.WriteLine("Class Network.DfsInfo()");

         DumpClassDfsInfo();
      }

      #endregion // Class_Network_DfsInfo

      #region Class_Network_DfsStorage

      [TestMethod]
      public void Class_Network_DfsStorage()
      {
         Console.WriteLine("Class Network.DfsStorage()");
         Console.WriteLine("\nPlease see unit test: Class_Network_DfsInfo()");
      }

      #endregion // Class_Network_DfsStorage

      #region Class_Network_OpenConnectionInfo

      [TestMethod]
      public void Class_Network_OpenConnectionInfo()
      {
         Console.WriteLine("Class Network.OpenConnectionInfo()");

         DumpOpenConnectionInfo(LocalHost);
         //DumpOpenConnectionInfo(MyServer);
      }

      #endregion // Class_Network_OpenConnectionInfo

      #region Class_Network_OpenResourceInfo

      [TestMethod]
      public void Class_Network_OpenResourceInfo()
      {
         Console.WriteLine("Class Network.OpenResourceInfo()");
         
         DumpClassOpenResourceInfo(LocalHost, LocalHostShare);
         //DumpClassOpenResourceInfo(MyServer, MyServerShare);
      }

      #endregion // Class_Network_OpenResourceInfo

      #region Class_Network_ShareInfo

      [TestMethod]
      public void Class_Network_ShareInfo()
      {
         Console.WriteLine("Class Network.ShareInfo()");

         DumpClassShareInfo(LocalHost);
         DumpClassShareInfo(MyServer);
      }

      #endregion // Class_Network_ShareInfo

      #endregion // Network

      #region OperatingSystemInfo

      #region Class_OperatingSystemInfo

      [TestMethod]
      public void Class_OperatingSystemInfo()
      {
         Console.WriteLine("Class OperatingSystemInfo()\n");

         Console.WriteLine("\tOsVersionName        : [{0}]", OperatingSystemInfo.OsVersionName);
         Console.WriteLine("\tOsVersion            : [{0}]", OperatingSystemInfo.OsVersion);
         Console.WriteLine("\tServicePackVersion   : [{0}]", OperatingSystemInfo.ServicePackVersion);
         Console.WriteLine("\tIsServer             : [{0}]", OperatingSystemInfo.IsServer);
         Console.WriteLine("\tProcessorArchitecture: [{0}]", OperatingSystemInfo.ProcessorArchitecture);

         Console.WriteLine("\n\tOperatingSystemInfo.IsAtLeast()\n");

         Console.WriteLine("\t\tOS Earlier           : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.Earlier));
         Console.WriteLine("\t\tWindows 2000         : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.Windows2000));
         Console.WriteLine("\t\tWindows XP           : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.WindowsXp));
         Console.WriteLine("\t\tWindows Vista        : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.WindowsVista));
         Console.WriteLine("\t\tWindows 7            : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.Windows7));
         Console.WriteLine("\t\tWindows 8            : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.Windows8));
         Console.WriteLine("\t\tWindows 8.1          : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.Windows81));

         Console.WriteLine("\t\tWindows Server 2003  : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.WindowsServer2003));
         Console.WriteLine("\t\tWindows Server 2008  : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.WindowsServer2008));
         Console.WriteLine("\t\tWindows Server 2008R2: [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.WindowsServer2008R2));
         Console.WriteLine("\t\tWindows Server 2012  : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.WindowsServer2012));
         Console.WriteLine("\t\tWindows Server 2012R2: [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.WindowsServer2012R2));

         Console.WriteLine("\t\tOS Later             : [{0}]", OperatingSystemInfo.IsAtLeast(OsVersionName.Later));
      }

      #endregion // Class_OperatingSystemInfo

      #endregion // OperatingSystemInfo

      #endregion Unit Test Callers
   }
}