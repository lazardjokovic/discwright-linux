# What the Linux AppImage is made from that is not ours, each named by the
# hash of the exact file that was measured (DiscWright-Lab/linux-plan/spike-4.md,
# 2026-11-16). Read by tools/make-the-linux-appimage.sh and by the check.
#
# Three files come from outside while the AppImage is put together. A
# download whose hash is not the one here stops the build: what goes inside
# a file people run is not left to whatever an address serves that day.
#
# To move any of them on: fetch the new file, read what it is, put its hash
# here, and run tools/see-the-linux-appimage-run.sh, which puts the xorriso
# inside through DiscWright's own tests.

# --- xorriso, which DiscWright runs to write a disc image -------------------
#
# GNU xorriso, the one source file that holds libburn, libisofs and
# libisoburn together. GPL version 3 or later, so its licence and where its
# source is go inside the AppImage with it.
XORRISO_VERSION=1.5.6.pl02
XORRISO_FILE=xorriso-1.5.6.pl02.tar.gz
XORRISO_URL=https://ftp.gnu.org/gnu/xorriso/xorriso-1.5.6.pl02.tar.gz
XORRISO_SHA256=786f9f5df9865cc5b0c1fecee3d2c0f5e04cab8c9a859bd1c9c7ccd4964fdae1
XORRISO_UNPACKS_TO=xorriso-1.5.6

# Everything optional left out, so it needs no library but the C one, and
# that one is linked in. Nothing DiscWright asks of xorriso uses any of
# these: the 68 tests that run a real xorriso pass without them.
XORRISO_CONFIGURED="--disable-libreadline --disable-libedit --disable-libacl --disable-xattr --disable-zlib --disable-libcdio --disable-external-filters --disable-launch-frontend"

# --- what turns a folder into an AppImage ------------------------------------
APPIMAGETOOL_VERSION=1.9.1
APPIMAGETOOL_URL=https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage
APPIMAGETOOL_SHA256=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0

# --- the few hundred kilobytes at the front of every AppImage ----------------
#
# Handed to appimagetool by name. Left to itself it fetches one from a
# release called "continuous", which is a different file from one month to
# the next.
RUNTIME_VERSION=20251108
RUNTIME_URL=https://github.com/AppImage/type2-runtime/releases/download/20251108/runtime-x86_64
RUNTIME_SHA256=2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d
