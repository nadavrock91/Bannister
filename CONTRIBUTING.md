# Contributing to Bannister

First off, thanks for taking the time to contribute! 🎉

## How Can I Contribute?

### 🐛 Reporting Bugs

Before creating a bug report, please check existing issues to avoid duplicates.

**When reporting a bug, include:**
- A clear, descriptive title
- Steps to reproduce the issue
- Expected vs actual behavior
- Screenshots if applicable
- Your environment (OS, .NET version, device)

### 💡 Suggesting Features

Feature requests are welcome! Please:
- Use a clear, descriptive title
- Describe the problem you're trying to solve
- Explain your proposed solution
- Consider if it fits the app's philosophy (gamification for personal development)

### 🔧 Pull Requests

1. **Fork & Clone**
   ```bash
   git clone https://github.com/YOUR_USERNAME/Bannister.git
   cd Bannister
   ```

2. **Create a Branch**
   ```bash
   git checkout -b feature/your-feature-name
   # or
   git checkout -b fix/bug-description
   ```

3. **Make Your Changes**
   - Follow the existing code style
   - Add comments for complex logic
   - Update documentation if needed

4. **Test Your Changes**
   - Build and run on your target platform
   - Test edge cases
   - Ensure existing features still work

5. **Commit**
   ```bash
   git commit -m "Add: brief description of changes"
   ```
   
   Commit message prefixes:
   - `Add:` New feature
   - `Fix:` Bug fix
   - `Update:` Improvements to existing features
   - `Refactor:` Code changes that don't affect behavior
   - `Docs:` Documentation only

6. **Push & Create PR**
   ```bash
   git push origin feature/your-feature-name
   ```
   Then open a Pull Request on GitHub.

## 📁 Project Structure

```
Bannister/
├── Models/           # Data models - SQLite entities
├── Services/         # Business logic layer
├── Views/            # MAUI pages (XAML + code-behind)
├── ViewModels/       # View models for complex pages
├── Helpers/          # Utility classes
├── Converters/       # XAML value converters
├── Drawables/        # Custom graphics (charts)
└── ConversationPractice/  # Standalone module
```

## 🎨 Code Style Guidelines

### C# Conventions
- Use `PascalCase` for public members, `_camelCase` for private fields
- Prefer `var` when type is obvious
- Use meaningful names (not `x`, `temp`, etc.)
- Keep methods focused and under ~50 lines when possible

### MAUI/XAML
- Prefer code-behind for dynamic UIs (this project uses minimal XAML)
- Use consistent spacing and indentation
- Group related UI elements with comments

### Example
```csharp
// Good
private readonly AuthService _auth;
private List<ActivityGameViewModel> _allActivities = new();

public async Task LoadActivitiesAsync()
{
    var activities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _gameId);
    // Process activities...
}

// Avoid
private AuthService a;
private List<ActivityGameViewModel> list;

public async Task Load()
{
    var x = await _activities.GetActivitiesAsync(a.CurrentUsername, g);
}
```

## 🧪 Testing

Currently the project doesn't have automated tests. This is a great area for contribution!

If adding tests:
- Use xUnit for unit tests
- Name tests descriptively: `MethodName_Scenario_ExpectedResult`
- Mock services when testing ViewModels

## 📝 Documentation

- Update README.md for new features
- Add XML comments to public methods
- Include code comments for complex logic

## 🤔 Questions?

- Open a Discussion on GitHub
- Tag your issue with `question`

## 🏆 Recognition

Contributors will be recognized in:
- README.md acknowledgments section
- Release notes for significant contributions

Thank you for helping make Bannister better! 🐉
