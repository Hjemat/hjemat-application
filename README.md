# Hjemat Master Application
Program to manage connected devices.

# Building and running on OS X and Linux
Requires Mono

Go to root directory. Restore required packages

`nuget restore Hjemat.sln`

Build with xbuild

`xbuild`

The resulting binary should be at `Hjemat/bin/Debug/Hjemat.exe`. Run it with 

`mono Hjemat.exe`

This will generate a `settings.json` file in the directory. Edit it to match your setup. You most certainly have to change the port name as it is a Windows port name as default.

If the program for some reason doesn't write or read from the port properly, you may have to run the program with `sudo`
