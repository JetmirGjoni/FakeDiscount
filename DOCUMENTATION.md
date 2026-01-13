# Fake Discount Detector - System Documentation

## 1. Introduction

### 1.1 Project Overview
The **Fake Discount Detector** is a specialized tool designed to bring transparency to e-commerce pricing. By tracking product prices over time and analyzing their history, the system identifies "fake discounts"—instances where a retailer artificially inflates a product's price shortly before a sale event to make the discount appear larger than it actually is.

### 1.2 Key Features
- **Multi-Site Scraping**: Configurable scrapers for various e-commerce platforms (Gjirafa50, Foleja, etc.).
- **Price History Tracking**: Records price points over time to build a historical dataset.
- **Hybrid AI Classification**: Categorizes products using a local ML.NET model, falling back to a Large Language Model (Google Gemini) for low-confidence predictions.
- **Active Learning**: Automatically improves the local model by feeding back LLM corrections into the training set.
- **Fake Discount Detection**: Algorithmic analysis of price trends to flag deceptive pricing practices.
- **Distributed Architecture**: Scalable worker system capable of running distributed scraping tasks via RabbitMQ.

### 1.3 Technology Stack
- **Framework**: .NET 9 (C#)
- **Database**: SQLite (Local) / Compatible with SQL Server
- **Web Scraping**: PuppeteerSharp (Headless Chrome)
- **Machine Learning**: ML.NET (Local Classification)
- **AI Integration**: Google Gemini API (Fallback & Enhancement)
- **Messaging**: RabbitMQ (Optional for distributed mode), In-Memory Channels (Local mode)

---

## 2. System Architecture

The system is built on a modular "Clean Architecture" principle, separating the domain logic from infrastructure details.

### 2.1 High-Level Architecture
The solution consists of three main runnable projects and two shared libraries:

1.  **FakeDiscountDetector.Core**: The heart of the system. Contains domain entities (`Product`, `PriceRecord`), interfaces (`IScraper`, `IProductRepository`), and configuration models. Dependencies are minimal.
2.  **FakeDiscountDetector.Infrastructure**: Implements the interfaces defined in Core. Includes:
    - `Scraping`: *GenericConfigurableScraper* using Puppeteer.
    - `Data`: Entity Framework Core *AppDbContext*.
    - `AI`: *MLProductClassifier*, *GeminiFallbackService*, and *HybridClassifier*.
    - `Messaging`: *RabbitMQService*.
3.  **FakeDiscountDetector.Worker**: A Background Service (Daemon) responsible for executing scraping tasks. It consumes messages from the queue (or in-memory channel), scrapes the target site, classifies products, and saves data to the DB.
4.  **FakeDiscountDetector.Web**: An ASP.NET Core MVC application providing the user interface to view products, price histories, and detected fake discounts.

### 2.2 Data Flow

1.  **Scheduling**: The `SchedulingWorker` reads *scrapers.json* and publishes scraping tasks to the message queue.
2.  **Scraping**: The `ScrapingWorker` consumes a task. It launches a headless browser to render the target URL.
3.  **Extraction**: Product data (Name, Price, Image, OldPrice) is extracted using CSS selectors defined in the configuration.
4.  **Classification**:
    - The system checks if the product exists in the DB.
    - If new or uncategorized, it attempts to predict the category using the local **ML.NET model**.
    - If confidence is low (< 70%), it calls the **Gemini API**.
    - The result from Gemini is saved to the product AND added to *training_data.csv* for future model retraining.
5.  **Storage**: The product and its current price point are saved to the `Products` and `PriceRecords` tables in SQLite.
6.  **Analysis**: The `DiscountAnalyzer` checks the new price against the product's history to determine if the current discount is genuine or fake.

### 2.3 Domain Entities
- **Product**: Represents a unique item found on a store. Key fields: `Url` (Unique Index), `Name`, `StoreName`, `Category`.
- **PriceRecord**: A snapshot of a product's price at a specific time. Key fields: `Price`, `OriginalPrice` (claimed by store), `Timestamp`.

---

## 3. Getting Started

### 3.1 Prerequisites
- **.NET 9 SDK**: Required to build and run the application.
- **Google Gemini API Key**: Required for the AI fallback classification.
- **Internet Connection**: For scraping and accessing the Gemini API.
- **RabbitMQ (Optional)**: If you intend to run in distributed mode.

### 3.2 Installation
1.  **Clone the Repository**:
    ```bash
    git clone https://github.com/your-repo/FakeDiscountDetector.git
    cd FakeDiscountDetector
    ```
2.  **Restore Dependencies**:
    ```bash
    dotnet restore
    ```
3.  **Setup Configuration**:
    - Navigate to `FakeDiscountDetector.Worker`.
    - Create or update *appsettings.json* (see Configuration below).

### 3.3 Configuration

*appsettings.json*:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=../data/fakediscount.db"
  },
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "UserName": "guest",
    "Password": "guest"
  }
}
```

*scrapers.json*:
This file defines the target websites. It is located in *FakeDiscountDetector.Infrastructure/Scraping/*.
```json
[
  {
    "Name": "Gjirafa50",
    "BaseUrl": "https://gjirafa50.com/",
    "CategorySelector": "ul.menu-vertical > li > a",
    "ItemSelector": ".product-box",
    "NameSelector": ".product-title",
    "PriceSelector": ".price-current",
    "PaginationType": "NextPageLink",
    "MaxPages": 5
  }
]
```

### 3.4 Running the Application
**Local Mode (Simpler)**:
Just run the Worker project. It will default to in-memory queuing if `--use-rabbitmq` is not passed.
```bash
cd FakeDiscountDetector.Worker
dotnet run
```

**Distributed Mode**:
1.  Ensure RabbitMQ is running.
2.  Run the worker with the flag:
```bash
dotnet run -- --use-rabbitmq

**Running the Web Interface**:
Open a new terminal and run:
```bash
cd FakeDiscountDetector.Web
dotnet run
```
The application will be available at `http://localhost:5038` (or similar port shown in logs).

---

## 4. Component Details

### 4.1 Scraping Engine (*GenericConfigurableScraper*)
The scraper is designed to be website-agnostic. Instead of writing code for each site, you write configuration.
- **Headless Browser**: Uses `PuppeteerSharp` to launch a headless Chrome instance. This bypasses many anti-bot measures that block simple HTTP requests and allows scraping of JavaScript-rendered content (React/Vista/Angular).
- **Resource Optimization**: Blocks images and fonts to speed up page loads and save bandwidth.
- **Discovery Mode**: If a `CategorySelector` is provided, the scraper first visits the `BaseUrl`, collects all category links, and creates new sub-tasks for each category.

### 4.2 AI Classification System
The system uses a **Hybrid Classifier** (*HybridClassifier.cs*) to categorize products (e.g., "Smartphone", "Laptop", "Monitor").

1.  **Level 1: Local ML.NET Model**:
    - Fast and offline.
    - Uses a `SdcaMaximumEntropy` multiclass classifier.
    - Trained on *data/training_data.csv*.
2.  **Level 2: Gemini API Fallback**:
    - If the local model's confidence is < 0.7, the product is sent to Google Gemini.
    - Prompt: *"Classify this product into one of the following categories: [list]..."*
3.  **Active Learning Loop**:
    - When Gemini provides a label, it is **automatically appended** to *training_data.csv*.
    - The local model can be retrained to "learn" from Gemini's judgment, reducing future API costs.

### 4.3 Discount Analysis (*DiscountAnalyzer*)
Determines if a discount is "Fake" based on heuristics:
- **Price Check**: If `CurrentPrice` is close to or higher than the `AveragePrice` over the last 30 days, despite a "Sale" label.
- **Markup-then-Discount**: Detects if the price was raised significantly just before the discount was applied.

### 4.4 Web Interface (*FakeDiscountDetector.Web*)
The user interface is an ASP.NET Core MVC application.
-   **Dashboard**: Displays the "Biggest Discounts Today" and alerts for "Potential Fake Discounts".
-   **Smart Search**: Includes an auto-complete search bar to find products quickly.
-   **Store Filtering**: Allows filtering products by their source website (e.g., Gjirafa50, Foleja).
-   **Product Details**: Shows the full price history chart and allows manual classification override.


---

## 5. API & Integration

### 5.1 Message Queue (RabbitMQ)
In distributed mode, the system relies on a message queue to distribute scraping tasks.
- **Queue Name**: `scraping_tasks`
- **Payload Format**: JSON serialized `ScraperConfig` object.
    ```json
    {
      "Name": "StoreName",
      "BaseUrl": "...",
      "TargetUrl": "SpecificCategoryUrl"
    }
    ```

### 5.2 External APIs (Google Gemini)
- **Endpoint**: `https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent`
- **Usage**: Used strictly for classification fallback.
- **Rate Limits**: The system does not currently implement rate limiting for Gemini, so be mindful of the API quota.

---

## 6. Maintenance & Operations

### 6.1 Database Management
- **Location**: *data/fakediscount.db* (SQLite).
- **Migration**: The system uses EF Core `EnsureCreated()` at startup. For complex schema changes, use EF Core Migrations:
    ```bash
    dotnet ef migrations add <Name> --project ../FakeDiscountDetector.Infrastructure --startup-project ../FakeDiscountDetector.Worker
    dotnet ef database update
    ```
- **Backup**: Regularly copy the *fakediscount.db* file to a secure location (ensure the app is stopped to prevent corruption).

### 6.2 Application Logs
Logs are output to the Console (stdout/stderr).
- **ScrapingWorker Logs**:
    - `[Information]`: Task start, products found, new product added.
    - `[Warning]`: Potential fake discount detected (`LogPotentialFakeDiscount`).
    - `[Error]`: Scraping failures (timeouts, selector errors).

### 6.3 Troubleshooting
- **Scraper returns 0 products**:
    - Check if the site layout changed. Update selectors in *scrapers.json*.
    - Check if the site is blocking the headless browser (try updating User-Agent string in *GenericConfigurableScraper.cs*).
- **Classification is always "Unknown"**:
    - Ensure *model.zip* exists in the Worker directory.
    - Check if Gemini API Key is valid (for fallback).

## 7. Developer Guide

### 7.1 Adding a New Scraper
1.  Open *FakeDiscountDetector.Infrastructure/Scraping/scrapers.json*.
2.  Add a new JSON object with the site's CSS selectors.
3.  Restart the Worker service.

### 7.2 Retraining the Model
The model usually updates itself via Active Learning. To force a full retrain from the CSV:
1.  Ensure *training_data.csv* is populated.
2.  Call the `ITrainingService.TrainModelAsync()` method (currently triggered via `SchedulingWorker` or manually via a temporary code execution).


