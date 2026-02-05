# Backend Startup Instructions

## Quick Start (Development with SQLite)

1. **Install Prerequisites**:
   ```bash
   # Install .NET 8.0 SDK
   # Install Tesseract OCR (for your platform)

   # Windows (using Chocolatey):
   choco install tesseract

   # Ubuntu/Debian:
   sudo apt-get install tesseract-ocr tesseract-ocr-eng libtesseract-dev

   # macOS (using Homebrew):
   brew install tesseract tesseract-lang
   ```

2. **Setup Database**:
   ```bash
   # This will automatically create and migrate SQLite database
   dotnet ef database update --context DocumentVerificationDbContext
   ```

3. **Download AI Model** (Optional - face recognition will be disabled without it):
   ```bash
   # Download ArcFace model from:
   # https://github.com/onnx/models/tree/main/vision/body_analysis/arcface

   # Place the downloaded .onnx file in: models/arcface_resnet100.onnx
   ```

4. **Run the Backend**:
   ```bash
   dotnet run
   ```

## Production Setup (PostgreSQL)

1. **Install PostgreSQL**:
   ```bash
   # Ubuntu/Debian:
   sudo apt-get install postgresql postgresql-contrib

   # macOS:
   brew install postgresql

   # Windows:
   # Download and install from postgresql.org
   ```

2. **Create Database**:
   ```sql
   CREATE DATABASE DocumentVerification;
   CREATE USER docverify WITH PASSWORD 'secure_password';
   GRANT ALL PRIVILEGES ON DATABASE DocumentVerification TO docverify;
   ```

3. **Update Connection String**:
   Edit `appsettings.json` and update the PostgreSQL connection string with your credentials.

4. **Run Migrations**:
   ```bash
   dotnet ef database update --context DocumentVerificationDbContext
   ```

## Access Points

Once running:
- **API**: http://localhost:5000
- **Swagger Documentation**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/health

## Default Admin User

The backend will create a default admin user on first run:
- **Email**: admin@docverify.com
- **Password**: Admin123!

## Troubleshooting

### Tesseract Issues
- Ensure Tesseract is installed and in your PATH
- On Windows, you may need to add Tesseract to system PATH
- Download additional language files if needed

### Database Issues
- For PostgreSQL: Ensure the database server is running
- For SQLite: Ensure write permissions to the backend directory

### ONNX Model Issues
- The app will start without the ONNX model, but face recognition will be disabled
- Download the model from the ONNX Model Zoo if face recognition is needed

### Port Issues
- The app defaults to port 5000
- Change the port in `Properties/launchSettings.json` if needed