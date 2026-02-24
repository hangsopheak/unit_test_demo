# FoodFast: Specification & Unit Testing Lab

**Course:** Software Testing

Welcome to hands-on lab for Software Testing. In this session, we are bridging gap between business requirements and executable code. You will act as a Quality Assurance Engineer evaluating the `DeliveryPricingEngine` for a food delivery platform.

## 🏗️ Project Scaffolding

To bootstrap this .NET 10 solution on your machine (whether macOS or Windows), open your terminal and run the following commands.

```bash
# 1. Create the root directory and enter it
mkdir FoodFastLab
cd FoodFastLab

# 2. Create the blank Solution file
dotnet new sln -n FoodFast

# 3. Create the Core (Logic), Console, and Tests projects
# Note: Ensure you have the .NET 10 SDK installed
dotnet new classlib -o src/FoodFast.Core -f net10.0
dotnet new console -o src/FoodFast.ConsoleApp -f net10.0
dotnet new xunit -o tests/FoodFast.Tests -f net10.0

# 4. Add all projects to the Solution
dotnet sln add src/FoodFast.Core/FoodFast.Core.csproj
dotnet sln add src/FoodFast.ConsoleApp/FoodFast.ConsoleApp.csproj
dotnet sln add tests/FoodFast.Tests/FoodFast.Tests.csproj

# 5. Link the Console and Test projects to the Core project
dotnet add src/FoodFast.ConsoleApp/FoodFast.ConsoleApp.csproj reference src/FoodFast.Core/FoodFast.Core.csproj
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

# Build the Console project
dotnet build src/FoodFast.ConsoleApp/FoodFast.ConsoleApp.csproj

# Build the Tests project
dotnet build tests/FoodFast.Tests/FoodFast.Tests.csproj
```

### Run the Console Application

To run the FoodFast console application and interactively test the pricing engine:

```bash
dotnet run --project src/FoodFast.ConsoleApp/FoodFast.ConsoleApp.csproj
```

The interactive console app will prompt you to:

- Enter cart subtotal (in dollars)
- Enter delivery distance (in kilometers)
- Specify if it's rush hour (y/n)

After entering your values, the app will display:

- Order details (cart subtotal, distance, rush hour status)
- Calculated delivery fee

You can continue calculating more orders or exit at any time.

**Try testing the boundary bug**: Enter exactly `50.00` as the cart subtotal to see the intentional bug (should be free but will charge a fee).

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

## 📚 Documentation

- **[Business Specification](docs/BUSINESS_SPECIFICATION.md)** - Detailed business rules and example scenarios
- **[Test Case Brainstorming](docs/TEST_CASE_BRAINSTORMING.md)** - Comprehensive test case planning by testing technique
