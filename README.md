# GoWork API

GoWork is a robust and scalable backend API designed for a comprehensive job portal and recruitment platform. It handles the complete lifecycle of job postings, applicant tracking, interview scheduling, user account management, and administrative dashboards.

## Features

- **Authentication & Authorization:** Secure registration, login, password resets, and email verification using ASP.NET Core Identity and JWT tokens (stored in HttpOnly cookies for enhanced security). Supports role-based access control (RBAC).
- **Job Management:** Employers can create, update, and manage job listings. Job seekers can search and filter through available opportunities.
- **Applicant Tracking System (ATS):** Seamless application process for candidates and structured application tracking for employers.
- **Interview Scheduling:** Built-in functionalities to schedule, manage, and track candidate interviews.
- **File Management:** Secure uploading and retrieval of user resumes (CVs) and company logos using Azure Blob Storage.
- **Real-time Notifications:** Push notifications delivered to mobile and web clients using Firebase Cloud Messaging (FCM).
- **Background Processing:** Automated, scheduled tasks (such as job expiration and maintenance) powered by Hangfire.
- **Admin Dashboard:** Endpoints providing detailed analytics and statistics for platform administrators.
- **AI Integrations:** Features leveraging OpenAI and PDF parsing (PdfPig) for intelligent CV processing and matching capabilities.

## Technologies & Libraries Used

- **Framework:** .NET 8 / ASP.NET Core Web API
- **Language:** C#
- **Database:** Microsoft SQL Server
- **ORM:** Entity Framework Core 8 (`Microsoft.EntityFrameworkCore.SqlServer`)
- **Authentication:** ASP.NET Core Identity & JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **Cloud Storage:** Azure Blob Storage (`Azure.Storage.Blobs`)
- **Background Jobs:** Hangfire (`Hangfire.AspNetCore`, `Hangfire.SqlServer`)
- **Push Notifications:** Firebase Admin SDK (`FirebaseAdmin`)
- **Email Service:** MailKit (`MailKit`)
- **AI & Document Processing:** OpenAI API (`OpenAI`), PdfPig (`PdfPig`)
- **API Documentation:** Swagger / OpenAPI (`Swashbuckle.AspNetCore`)
- **JSON Serialization:** Newtonsoft.Json

## Project Architecture

The project follows a layered architecture to maintain separation of concerns:

- **`Controllers/`**: API endpoints grouped by feature areas (e.g., `Auth`, `Dashboard`, `JobController`, `Mobile`).
- **`Services/`**: Core business logic and integrations (e.g., `AccountService`, `JobService`, `InterviewService`, `FileService`, `FirebaseNotificationSender`).
- **`Data/`**: Contains the Entity Framework `ApplicationDbContext` and database `Migrations`.
- **`Models/` & `DTOs/`**: Domain models (entities mapped to the database) and Data Transfer Objects (used for API requests and responses).
- **`Authorization/`**: Custom authorization policies, handlers, and requirements (e.g., `CandidateOwnInterviewRquirements`).
- **`Infrastructure/`**: Configuration for infrastructure concerns like Hangfire setups.

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (LocalDB or an external instance)
- Azure Storage Account (for Blob Storage)
- Firebase Project (for Push Notifications)
- SMTP Server Credentials (for Email services)
- OpenAI API Key

### Setup Instructions

1. **Clone the repository:**
   ```bash
   git clone <repository-url>
   cd GoWork
   ```

2. **Configure Settings:**
   Update the `appsettings.json` file with your specific configuration details, including:
   - `ConnectionStrings:DefaultConnection` (SQL Server connection string)
   - `JWT` (Issuer, Audience, and Secret Key)
   - `Firebase:CredentialsPath` (Path to your `firebase-adminsdk.json` file)
   - Azure Storage, SMTP, and OpenAI credentials if specified in the configuration.

3. **Apply Database Migrations:**
   Ensure the database is up-to-date by running EF Core migrations:
   ```bash
   dotnet ef database update
   ```

4. **Run the Application:**
   ```bash
   dotnet run
   ```
   The API will start and you can explore the endpoints via the Swagger UI interface (usually at `https://localhost:<port>/swagger` during development).

## CORS Policy

The API is configured to allow requests from the following trusted frontend origins:
- `http://localhost:3000` (Local Development)
- `https://go-work-next-js.vercel.app` (Next.js Web Frontend)
- `https://masarak.app`