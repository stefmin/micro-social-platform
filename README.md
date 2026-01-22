# Micro-Social Platform ASP.NET MVC

## Setup

### - clone latest version

### - run the following commands in order to setup database url:
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "{yourDatabaseConnectionString};"
```

### - run the following commands to setup S3 storage:
```bash
dotnet user-secrets set "S3:ServiceURL" "{yourS3ServiceUrl}"
dotnet user-secrets set "S3:AccessKey" "{yourAccessKey}"
dotnet user-secrets set "S3:SecretKey" "{yourSecretKey}"
dotnet user-secrets set "S3:BucketName" "{yourBucketName}"	
```

### - build and run the project
