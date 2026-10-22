# Third-party software

Better AirPlay ships these programs in its `receiver` folder. They run as a separate process
(`receiver\bin\uxplay.exe`) that the tray app starts and stops.

- **UxPlay**, GPL-3.0. The license is in `receiver\UXPLAY-LICENSE.txt`. The exact source commit
  is linked in `receiver\UXPLAY-SOURCE.txt`. Project page: https://github.com/FDH2/UxPlay
- **GStreamer** and its plugins, LGPL-2.1 or later (some plugins are GPL). https://gstreamer.freedesktop.org
- **FFmpeg** through gst-libav, built by MSYS2 (LGPL/GPL, depending on the build options).
  https://ffmpeg.org
- The other DLLs come from the MSYS2 UCRT64 package repository, each under its own license.
  Sources: https://packages.msys2.org
