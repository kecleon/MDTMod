﻿# MDTadusMod

MDTadusMod is a modern, cross-platform port of the popular tool Muledump for the game Realm of the Mad God. It is built using .NET MAUI and Blazor Hybrid, allowing it to run on Windows, macOS, Android, and iOS from a single codebase.

## Features

*   **Account Management**: Easily add and manage multiple Realm of the Mad God accounts.
*   **Character and Inventory Viewing**: View your characters, their stats, and all the items in your inventory and vaults.
*   **Cross-Platform**: Runs on Windows, macOS, Android, and iOS.
*   **Modern UI**: A clean and modern user interface built with Blazor.
*   **Asset Extraction**: Utilizes `RotMGAssetExtractor` to get the latest game assets.

## Tech Stack

*   **.NET 8**
*   **.NET MAUI**
*   **Blazor Hybrid**
*   **C#**

## Project Structure

The solution includes two primary projects:
*   **MDTadusMod**: The main .NET MAUI application.
*   **[RotMGAssetExtractor](https://github.com/TadusPro/RotMGAssetExtractor)**: A class library responsible for extracting game assets. `MDTadusMod` includes a project reference to `RotMGAssetExtractor`. When you open the solution in Visual Studio, this reference should be automatically resolved.

## Getting Started

Follow these instructions to get a copy of the project up and running on your local machine for development and testing purposes.

### Prerequisites

*   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
*   .NET MAUI workload. You can install it by running the following command:
    ```shell
    dotnet workload install maui
    ```
*   Visual Studio 2022 with the .NET Multi-platform App UI development workload installed.

### Installation

1.  Clone the repository:
    ```shell
    git clone https://github.com/TadusPro/MDTadusMod
    ```
2.  Open the solution file (`.sln`) in Visual Studio 2022.

## Building and Running

1.  Set the startup project to `MDTadusMod`.
2.  Select the target framework/platform (e.g., Windows Machine, Android Emulator, or a physical device).
3.  Press F5 or click the "Start" button to build and run the application.

## Acknowledgements

This project is a port of the original Muledump and would not be possible without the work of its creators and maintainers. Special thanks to:

*   **[atomizer](https://github.com/atomizer/muledump)** for the original creation of Muledump.
*   **[jakcodex](https://github.com/jakcodex/muledump)** for their long-term support and maintenance.
*   **[BR-](https://github.com/BR-/muledump)** for continuing support.
*   **[faynt](https://github.com/faynt0/muledump-but-better)** for continuing support.

