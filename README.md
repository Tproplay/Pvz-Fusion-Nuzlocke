# PvzRHNuzlocke

A Nuzlocke-style mod for **Plants vs. Zombies Fusion (RH)**. This mod introduces permanent consequences to your gameplay: when a plant dies, it and all its fusion ancestors are banned from your seed library for the rest of the run.

## 🚀 Features

- **Permanent Death:** If a plant is eaten, shoveled, or crushed, you lose access to it.
- **Recursive Banning:** Losing a Fusion plant also bans the specific parents used to create it.
- **Automatic Persistence:** Your progress (banned plants and discovered recipes) is saved automatically in your `UserData` folder.
- **Seed Selection Lockout:** Banned plants are hidden or unclickable in the seed selection menu.

## 🛠️ Installation

1. Ensure you have [MelonLoader](https://melonloader.com) installed for PvZ Fusion.
2. Download the `PvzRHNuzlocke.dll` from the [Releases](#) page.
3. Place the DLL into your game's `Mods` folder.
4. Run the game!

## 💻 For Developers (Building from Source)

This project uses a custom `GamePath` variable in the `.csproj` to manage dependencies across different machines.

1. Clone the repository.
2. Locate the `PvzRHNuzlocke.csproj` file.
3. Open the file and update the `<GamePath>` property to point to your PvZ Fusion installation directory:
   ```xml
   <PropertyGroup>
     <GamePath>C:\Your\Path\To\Pvz\Fusion\Game Files</GamePath>
   </PropertyGroup>
4. Build the solution using **Visual Studio**.
## 📂 Data Storage
The mod saves data in JSON format within the MelonLoader/UserData directory:
**NuzlockeBannedPlants.json:** Tracks which IDs are currently extinct.
**NuzlockeRecipes.json:** Tracks discovered fusion combinations to apply recursive bans.
## ⚖️ License
Distributed under the MIT License. See LICENSE for more information.
