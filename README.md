## Micro Social Platform (Instagram-like) — ASP.NET Core MVC

A full-stack social web app that mimics key Instagram interactions: public/private profiles, follow requests, media posts, likes, comments, group discussions with moderators, and an admin layer for platform governance. Built as an MVC application with authentication + authorization, a relational database model, and an S3-compatible media storage layer.

### Highlights
- **Auth & Roles (ASP.NET Identity):** visitor / registered user / admin flows, secured routes, and role-based UI + permissions.
- **Profiles & Search:** partial-name search, profile pages with **public/private visibility**, editable bio + avatar.
- **Follow System (Instagram-style):** unidirectional follow requests, `Pending → Accepted/Rejected` behavior for private accounts.
- **Posts, Media & Interactions:** create/edit/delete posts, comments, and reactions (likes), ordered by recency.
- **Personalized Feed:** timeline built from followed users’ posts, ordered descending by date.
- **Groups & Moderation:** join requests, moderator approvals, group messages with ownership rules (edit/delete own content).
- **AI Content Filtering:** pre-publish moderation for posts/comments; blocks harmful text and returns user-friendly feedback.
- **S3 Media Storage:** image uploads backed by an **S3-compatible bucket** (configurable via secrets).
- **Database Versioning:** Entity Framework migrations to track schema changes and keep environments consistent.
- **Team Workflow:** Git/GitHub collaboration + Trello sprint planning (tickets, iterations, and deliverables).

### Tech Stack
- **Backend:** C#, ASP.NET Core MVC
- **Auth:** ASP.NET Identity
- **Data:** Entity Framework Core (+ Migrations)
- **Storage:** S3-compatible object storage (AWS S3 / S3-like providers)
- **UI:** Razor Views + static assets
- **AI:** moderation step integrated before publishing content

### Project Structure (high level)
- `Areas/Identity` — Identity pages & auth flows  
- `Controllers`, `Models`, `Views` — MVC separation  
- `Data` — DbContext & persistence  
- `Migrations` — schema history  
- `Services` / `Middleware` — integrations (e.g., S3, AI moderation, request handling)

### Setup (local)
1. Clone the repository.
2. Configure secrets:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "{yourDatabaseConnectionString};"

# S3 storage
dotnet user-secrets set "S3:ServiceURL" "{yourS3ServiceUrl}"
dotnet user-secrets set "S3:AccessKey" "{yourAccessKey}"
dotnet user-secrets set "S3:SecretKey" "{yourSecretKey}"
dotnet user-secrets set "S3:BucketName" "{yourBucketName}"

# Update migrations and run
dotnet ef database update
dotnet run
