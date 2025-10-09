<div align="center">
<img width="363" height="276" alt="fairykey-icon-dark-small-crop" src="https://github.com/user-attachments/assets/38239d84-62d1-4a81-9e85-bd91c210cd40" />

# Fairy Key: a virtual piano sheet scroller
</div>

Fairy Key is a virtual piano sheet reading tool for practicing playing piano using QWERTY with features such as auto-scroll, note highlighting, a library to organize sheets, and more! 

## Features
- **Auto-scroll:** Notes are highlighted as you play and the sheet scrolls automatically line by line
- **Unfocused Keyboard Input:** During **Play Mode**, Fairy Key is passed keyboard inputs while not focused so you can play along in your preferred virtual piano app (Roblox, FL Studio, etc.) while following sheets in Fairy Key!
- **Library:** Create, organize, and share sheets
	- Sheets are stored as `.txt` files with [optional metadata](#fairy-key-sheet-format-guide) such as the songs Title, Artist, and sheet creator.
	- Folder support
- **Pin mode:** Keep Fairy Key on top of other applications while you play
- **Noob mode:** Non-shifted notes count as correct notes
- Transpose support
- Resize window to fit sheet support
- Restart the sheet and scroll to the top at any time

## Installation
1. Download the latest release from [Releases](https://github.com/FairyKey/FairyKey/releases)
2. Extract the ZIP anywhere you like 
3. Run `FairyKey.exe` 

> Currently only Windows 10/11 are supported and [.NET 8.0 runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) is required to run.

## Usage

1. Launch `FairyKey.exe`
2. Use the `☰+` button to create a new sheet
3. Follow the [Fairy Key Sheet Format Guide](#fairy-key-sheet-format-guide)
4. After saving a sheet, it will appear in the **Library** panel
5. Click a sheet to load it
6. Press the **Play** button at the top to activate **Play Mode**
7. (*Recommended*) enable **Pin Mode** from the ☰ menu to overlay Fairy Key on top of your virtual piano application
8. Switch to your virtual piano application and play along with the sheet! 

> Applications running as Admin won't pass input to Fairy Key unless Fairy Key is also running as Admin (e.g. Roblox opened as Admin won't pass inputs to Fairy Key)

### Importing multiple sheet
1. Place your sheet `.txt` files in the `Sheets` folder (created in the same directory automatically on first launch)
2. Launch `FairyKey.exe`
3. Sheets will appear in the **Library**

> Fairy Key can automatically detect and load new **single** sheets without restarting the application.

---

## Fairy Key Sheet Format Guide
Fairy Key reads sheets from user-provided `.txt` files. In addition to virtual piano notation, sheets can include metadata, such as the Title, Artist, and creator of the sheet, as well as transposition data between lines. They follow the following format:

```
title=Clair De Lune
artist=Claude Debussy  
creator=Tevins
+1
[uo] [fh] [sf] [iO] [ad] [sf] [ad]  -
[uo] [ps] [ad] [ps] f s [yi] [oa] [ps] [oa]  
[tyi] p a p d p [ryio] p o [ety] i o i [Wryu]
```
### Adding Metadata
In order for Fairy Key to recognize metadata, the first three lines of any sheet can optionally include the following: Title, Artist, and sheet creator. 

The metadata format (first 3 lines of the `.txt` file) must follow the following formatting:
```
title=
artist=
creator=
```  
Metadata is **optional** and not required. If a sheet doesn't include metadata, Fairy Key will assume the sheet's title based on its filename.

### Adding Transposition
To add a transposition indicator in a song, simply add it as a single line starting either with `+` or `-` followed by the amount:
```
+2
```
Without specifying, the default transposition is always `0`. Transpositions can be added anywhere between or before notation lines.

Additional formats also [supported](https://github.com/FairyKey/FairyKey/discussions/3):
```
Transpose +1
Transposition: +2
-2 (Transpose 5)
transpose: +2
(Transpose +3)
transposition -3
```


### Adding Notes
Lastly, notes can be added after the metadata freely using virtual piano notation. Fairy Key recognizes playable notes and ignores non-playable notes such as spaces. Currently supported formats for chords are `[ ]` and `-` for spaces/pauses.

---

## Building

Fairy Key is a WPF application built with [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/) using [.NET 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).  To build and run the project locally:

1. **Clone the repository**
```
git clone https://github.com/FairyKey/FairyKey.git
```

2. **Go into the cloned directory and install dependencies**
```
cd fairykey
dotnet restore
```

3. **Build the project**
```
dotnet build
```

Launching the project can be done with the created `FairyKey.sln` file.

## Third-Party Libraries

Fairy Key uses the following third-party libraries:

| Library                                                                  | License | Source |
| ------------------------------------------------------------------------ | ------- | ------ |
| [Material.Icons.WPF](https://www.nuget.org/packages/Material.Icons.WPF/) | MIT     | NuGet  |
| [MouseKeyHook](https://www.nuget.org/packages/MouseKeyHook/)             | MIT     | NuGet  |

Full license texts are included in [`THIRD_PARTY_LICENSES.md`](https://github.com/FairyKey/FairyKey/blob/main/THIRD_PARTY_LICENSES.md).
