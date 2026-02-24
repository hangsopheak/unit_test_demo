# FoodFast: Specification & Unit Testing Lab

**Course:** Software Testing

Welcome to the hands-on lab for Software Testing. In this session, we are bridging the gap between business requirements and executable code. You will act as a Quality Assurance Engineer evaluating the `DeliveryPricingEngine` for a food delivery platform.

## 🏗️ Project Scaffolding

To bootstrap this .NET 10 solution on your machine (whether macOS or Windows), open your terminal and run the following commands.

```bash
# 1. Create the root directory and enter it
mkdir FoodFastLab
cd FoodFastLab

# 2. Create the blank Solution file
dotnet new sln -n FoodFast

# 3. Create the Core (Logic) and Tests projects
# Note: Ensure you have the .NET 10 SDK installed
dotnet new classlib -o src/FoodFast.Core -f net10.0
dotnet new xunit -o tests/FoodFast.Tests -f net10.0

# 4. Add both projects to the Solution
dotnet sln add src/FoodFast.Core/FoodFast.Core.csproj
dotnet sln add tests/FoodFast.Tests/FoodFast.Tests.csproj

# 5. Link the Test project to the Core project
dotnet add tests/FoodFast.Tests/FoodFast.Tests.csproj reference src/FoodFast.Core/FoodFast.Core.csproj
```

## 🚀 Running the Project

### Build the Solution

To build the entire solution, run:

```bash
dotnet build FoodFast.slnx
```

Or build individual projects:

```bash
# Build the Core project
dotnet build src/FoodFast.Core/FoodFast.Core.csproj

# Build the Tests project
dotnet build tests/FoodFast.Tests/FoodFast.Tests.csproj
```

### Run Tests

To execute all unit tests in the solution:

```bash
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj
```

For more detailed test output:

```bash
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --logger "console;verbosity=detailed"
```

To run a specific test (replace `TestMethodName` with the actual test method name):

```bash
dotnet test tests/FoodFast.Tests/FoodFast.Tests.csproj --filter "FullyQualifiedName~TestMethodName"
```

### Clean Build Artifacts

To remove all build artifacts (bin/obj folders):

```bash
dotnet clean FoodFast.slnx
```

### Restore NuGet Packages

To restore all NuGet packages:

```bash
dotnet restore FoodFast.slnx
```
