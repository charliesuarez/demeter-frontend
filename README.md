# Demeter-Frontend

## Overview

This is the project containing the desktop, mobile, and WASM frontends
for the Demeter hydroponics system.

This projects leverages cross-platform application deployment by
employing .NET, .NET MAUI, and Blazor for desktop, mobile, and WASM
support respectively.

## Initial Setup
### Requirements

- Visual Studio
- Go to the Visual Studio Installer and select the following:
  - Under "Workloads":
    - .NET Desktop Development
    - .NET Multi-platform App UI Development
    - ASP.NET and Web Development
  - Under "Installation Details":
    - 
- GitHub Desktop (if not comfortable with CLI)

### Open the Solution

Just click on `Demeter-Frontend.sln` to start working on the project.

## Resources
### Books to Read

- C# 12 in a Nutshell
  - Quick Refresher
  - Teaches LINQ, Networking, and Concurrent Programming
- C# 13 and .NET 9 – Modern Cross-Platform Development Fundamentals - Ninth Edition By Mark J. Price
  - Teaches ASP.NET and Blazor
- Exploring Blazor: Creating Server-side and Client-side Applications in .NET 9
  - More advanced Blazor content
- Introducing .NET MAUI: Build and Deploy Cross-Platform Applications Using C# and .NET 9.0 Multi-Platform App UI By Shaun Lawrence
  - Teaches MAUI

### Articles to Read

- MSDN - .NET Documentation - https://learn.microsoft.com/en-us/dotnet/
  - Note: This is everything related to C#.
- MSDN - ASP.NET Core Blazor - https://learn.microsoft.com/en-us/aspnet/core/blazor/
- MSDN - .NET MAUI - https://learn.microsoft.com/en-us/dotnet/maui/
- MSDN - Build a .NET MAUI Blazor Hybrid app - https://learn.microsoft.com/en-us/aspnet/core/blazor/hybrid/tutorials/maui
  - Note: This Visual Studio Solution is a .NET MAUI Blazor Hybrid app.

## Project Milestones
### UX

- [ ] Define user and business goals
- [ ] Create user stories
- [ ] Define information hierarchy
- [ ] Plan data relationships
- [ ] Define interaction models
- [ ] Lay out user workflows
- [ ] Subtract as needed

### UI

- [ ] Define visual identity and color scheme
- [ ] Define static and interaction elements
- [ ] Convert shadcn over to XAML and Blazor
- [ ] Set up a component library
- [ ] Decide on a layout grid
- [ ] Create layouts for
  - [ ] Desktop
  - [ ] Mobile
  - [ ] WASM
- [ ] Check alignment and spacing
- [ ] Select typography

### Networking

- [ ] Receive information using Sockets and Bluetooth
- [ ] Send commands using Sockets and Bluetooth

### Desktop Platforms

- [ ] Works on Windows
- [ ] Works on Linux
- [ ] Works on macOS

### Mobile Platforms

- [ ] Works on Android
- [ ] Works on iOS
- [ ] Works on Wasm
