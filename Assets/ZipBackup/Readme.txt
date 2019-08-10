Zip Backup is an editor extension that will backup your projects into a zip file automatically or manually, it will use 7z.exe, that comes with Unity, or Fastzip.exe (http://forum.unity3d.com/threads/android-faster-apk-creation-experimental.327755/), an tool created by Unity that zips files about 23x faster than normal applications.

If auto backup is enabled, the extension will automatically zip your project and store it inside a folder called "Backups" within your project folder on a fixed time span, unless you specify a custom location, if you do so, all your backups for all projects will be located at the folder you specified.

You can also backup manually by going to "Assets/Backup Now", or clicking on a button called "Backup Now" on the preferences window.

When the zipping ends, the extension logs how long it took and the size of the final file to the console.

Unfortunately, this extension only works on Windows, 7z.exe can run both on 32bit and 64bit, but Fastzip only works with 64bit machines.