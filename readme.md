# Zip Backup

[![Build Status](https://travis-ci.com/mukaschultze/unity-zip-backup.svg?branch=master)](https://travis-ci.com/mukaschultze/unity-zip-backup)
[![Asset Store page](https://img.shields.io/badge/Unity-Asset%20Store-brightgreen)](https://assetstore.unity.com/packages/tools/utilities/zip-backup-71979?aid=1100l4JUz&pubref=github)

Unity extension to backup projects into zip files using 7z or [fastzip](http://forum.unity3d.com/threads/android-faster-apk-creation-experimental.327755/).

## Asset Store Description

**This project is now maintained at [GitHub](https://github.com/mukaschultze/unity-zip-backup), the version published here is deprecated**

Zip Backup backups your projects into zip files automatically or manually, it uses *7z* and [*fastzip*](http://forum.unity3d.com/threads/android-faster-apk-creation-experimental.327755/) and can achieve compression times up to x23 times faster than other conventional zipping applications.

It comes with a handy auto backup tool that automatically zips your project from time to time, preventing you from losing your work on eventual power outrages, Unity crashes, etc.

You can also backup manually by going to "Assets/Backup Now" menu, or clicking on "Backup Now" in the preferences window.

When the zipping ends, the extension logs how long it took and the size of the final file to the console.

*Windows only*

## Screenshots

![Logs](Assets/Screenshots/screenshot2.png)

![Settings page](Assets/Screenshots/screenshot1.png)

## Known Issues

- Fast zip may fail to zip 3GB+ files, try using 7zip in these cases
