DiscWright for Linux
====================

DiscWright turns a GOG offline installer into a game disc: the game's own
icon and name in Windows Explorer, a menu when the disc goes in, and the
game's name and icon on a Linux desktop that reads them.

To put it on this computer, in a terminal in this folder:

    ./install.sh

That copies DiscWright into your home folder and adds it to the application
menu. It needs no password and touches nothing outside your account. To take
it out again:

    ./install.sh --uninstall

To try it without installing anything, run the program where it is:

    ./DiscWright

DiscWright makes its disc images with xorriso, which most distributions do
not install by default. install.sh says so if it is missing:

    Ubuntu, Debian, Mint:  sudo apt install xorriso
    Fedora:                sudo dnf install xorriso
    Arch, Manjaro:         sudo pacman -S libisoburn

This version builds the disc and its ISO. It does not burn yet: K3b, Brasero
or Xfburn will burn the ISO it makes.

https://discwright.com
