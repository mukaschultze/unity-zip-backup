Zip Backup is an editor extension that will backup your projects into a zip file automatically or manually, it uses 7z.exe, that comes with Unity, or Fastzip.exe (http://forum.unity3d.com/threads/android-faster-apk-creation-experimental.327755/), a tool created by Unity that zips files about 23x faster than other zipping applications.
If auto backup is enabled, the extension will automatically zip your project and store it inside a folder called "Backups" within your project folder on a fixed time span.
You can also manually backup by going to "Assets/Backup Now" menu item, or clicking on a button called "Backup Now" on the preferences window.
When the zipping ends, the extension logs how long it took and the size of the final file to the console.
This extension is meant for Windows only, 7z.exe can run both on 32bit and 64bit systems, but Fastzip only works with 64bit machines.

KNOWN ISSUES:
- Fast zip may fail to zip 3GB+ files, try using 7zip in these cases

CHANGELOG:

Version 1.0.1
- Added list of backed up folders
- Fixed error when logging the size of big files