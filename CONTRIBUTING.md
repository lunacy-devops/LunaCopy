# Contributing to LunaCopy

Thank you for your interest in improving LunaCopy! We welcome bug reports, feature requests, and code contributions.

## How to Contribute

1. **Fork and Clone**
   ```bash
   git clone https://github.com/lunacy-devops/LunaCopy.git
   cd LunaCopy
   ```

2. **Branching**
   Create a feature or bugfix branch:
   ```bash
   git checkout -b feature/my-cool-feature
   ```

3. **Coding Standards**
   - Follow standard C# and .NET naming conventions.
   - Keep P/Invoke declarations documented and centralized in `LunaCopy.Native.NativeMethods`.
   - Ensure thread-safety when touching settings (`ConfigService`) or clipboard access.
   - Run tests before submitting:
     ```bash
     dotnet test
     ```

4. **Pull Requests**
   - Describe the problem solved or the feature introduced.
   - Ensure your code compiles cleanly without warnings or errors.
