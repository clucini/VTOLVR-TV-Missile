- LODManager needs patch
- WeaponManager needs patch
- MFDManager needs a patch
- powered needs impl

- Check where SetOpticalTargeter is called
	- Maybe call set TVCamera there?
	- idk, could also just check all weapons

- MFDManager is an object - under MFD1
- It has children
- Those children have an "MFDPage"
	- Which is just named
	- They also have a special script on each
