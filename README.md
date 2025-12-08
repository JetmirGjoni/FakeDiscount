# Fake Discount Detector

This project has two main components:
1. **Web Application**: Displays the products and their price history.
2. **Worker Service**: Scrapes product data in the background.

## To have

- .NET 9.0 SDK

## How to Run

You need to run both the Web application and the Worker service. It is recommended to run them in separate terminal windows. Kill the pprocesses after stoping the app.

### 1. Run the Web Application

Open a terminal and run:

```bash
cd FakeDiscountDetector.Web
dotnet run
```

The application will be available at `http://localhost:5000` (or the port shown in the output).
This will also create the SQLite database `fakediscount.db` if it doesn't exist.

### 2. Run the Worker Service

Open a **new** terminal window and run:

```bash
cd FakeDiscountDetector.Worker
dotnet run
```

The worker will start scraping data and saving it to the shared database. 
## Project Structure

- **FakeDiscountDetector.Web**: The ASP.NET Core MVC web application.
- **FakeDiscountDetector.Worker**: The background worker service for scraping.
- **FakeDiscountDetector.Core**: Shared domain entities and interfaces.
- **FakeDiscountDetector.Infrastructure**: Data access and scraping implementation.
