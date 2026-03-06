# 🐉 Bannister

A gamification app for habit formation and personal development built with .NET MAUI. Turn your goals into dragons and slay them by reaching Level 100.

![.NET MAUI](https://img.shields.io/badge/.NET%20MAUI-512BD4?style=flat&logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Android%20%7C%20iOS-blue)
![License](https://img.shields.io/badge/License-MIT-green)

## ✨ Features

- **🎮 Gamified Progress** - Earn EXP for completing activities, level up from 1-100
- **🐉 Dragon Slaying** - Each goal is a "dragon" you slay when you reach Level 100
- **🔥 Streak Tracking** - Track consecutive days for habit-forming activities
- **📊 Visual Charts** - See your EXP and level progression over time
- **⏰ Auto-Award** - Schedule activities to auto-award daily, weekly, or monthly
- **🎯 Habit Targets** - Set target dates for forming new habits
- **💬 Conversation Practice** - Role-play scenarios for social skill building
- **📱 Cross-Platform** - Works on Windows, Android, and iOS

## 📸 Screenshots

<!-- Add screenshots here -->

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with MAUI workload
  - Or VS Code with MAUI extension

### Installation

1. Clone the repository
   ```bash
   git clone https://github.com/YOUR_USERNAME/Bannister.git
   cd Bannister
   ```

2. Restore dependencies
   ```bash
   dotnet restore
   ```

3. Run the app
   ```bash
   # Windows
   dotnet build -f net8.0-windows10.0.19041.0
   
   # Android
   dotnet build -f net8.0-android
   ```

### Building for Release

```bash
# Windows
dotnet publish -f net8.0-windows10.0.19041.0 -c Release

# Android APK
dotnet publish -f net8.0-android -c Release
```

## 🏗️ Project Structure

```
Bannister/
├── Models/           # Data models (Activity, Dragon, Game, etc.)
├── Services/         # Business logic (ExpService, DragonService, etc.)
├── Views/            # UI pages and components
├── ViewModels/       # View models for data binding
├── Helpers/          # Utility classes
├── Converters/       # XAML value converters
├── Drawables/        # Custom chart drawables
└── ConversationPractice/  # Conversation practice module
```

## 🎮 Core Concepts

### The EXP System
- Activities give EXP based on their "meaningful until level" (1-100)
- Formula: `EXP = MeaningfulLevel × 2`
- Level thresholds follow: `Threshold = A × Level^Power` where A=10, Power=2

### Dragons
- Each game has a "dragon" representing your goal
- Reaching Level 100 = slaying the dragon
- Track multiple attempts with failure/success history

### Habits
- Activities can have habit cadences: Daily, Weekly, or Monthly
- Streak tracking for consecutive completions
- Display day restrictions (show only on specific days)

## 🤝 Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Setup

1. Install Visual Studio 2022 with:
   - .NET MAUI workload
   - Android SDK (for mobile testing)

2. Open `Bannister.sln` in Visual Studio

3. Set startup project to `Bannister`

4. Select your target platform and run

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [.NET MAUI](https://dotnet.microsoft.com/apps/maui)
- Uses [SQLite-net](https://github.com/praeclarum/sqlite-net) for local storage
- Community Toolkit for MAUI

## 📬 Contact

- Create an issue for bugs or feature requests
- Discussions tab for questions and ideas

---

**Slay your dragons. Level up your life.** 🐉⚔️
