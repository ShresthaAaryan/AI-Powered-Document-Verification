## AI-Powered Document Verification

Full-stack project for AI-powered document and identity verification, with a .NET 8 backend and a Next.js 16 frontend.

### Project Structure

- **Backend** (`Backend/`): ASP.NET Core Web API  
  - Handles authentication, verification workflows, OCR, and face matching  
  - Uses SQLite for local development (or PostgreSQL in production)  
  - Exposes REST APIs and Swagger docs at `http://localhost:5000`
- **Frontend** (`Frontend/`): Next.js 16 (App Router, TypeScript)  
  - Dashboard, verification flows, review UI, and auth pages  
  - Talks to the backend API for all data and actions

### Prerequisites

- **Backend**
  - .NET 8 SDK
  - Tesseract OCR installed and on PATH
  - (Optional) PostgreSQL for production
- **Frontend**
  - Node.js 18+ (or Bun if you prefer)

### Running the Backend (Development, SQLite)

From the `Backend/` directory:

```bash
# Apply migrations (creates local SQLite DB)
dotnet ef database update --context DocumentVerificationDbContext

# Run the API
dotnet run
```

Backend will be available at:

- API: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`
- Health: `http://localhost:5000/health`

Default admin user:

- Email: `admin@docverify.com`
- Password: `Admin123!`

### Running the Frontend

From the `Frontend/` directory:

```bash
npm install
npm run dev
```

Frontend will be available at `http://localhost:3000` and expects the backend at `http://localhost:5000` (adjust API URLs/env vars if needed).

### AI Model Files (Not in Git)

Large AI model files such as `Backend/Models/glintr100.onnx` are **ignored by Git** to avoid GitHub size limits.  
To enable face recognition:

1. Download an ArcFace / face recognition ONNX model from the ONNX Model Zoo (for example, ArcFace ResNet100).  
2. Place it under `Backend/Models/` (matching the filename your code expects).  
3. Restart the backend.

The app will still run without the model, but face matching features will be disabled.

### Environment Configuration

- Backend config lives in `Backend/appsettings.json` and `appsettings.Development.json`.
- Frontend uses `.env*` files (ignored by Git) to configure API base URLs and other secrets.

