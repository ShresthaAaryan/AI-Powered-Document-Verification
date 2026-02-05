# AI-Powered Document Verification Platform

A comprehensive document verification platform that uses AI and machine learning to authenticate identity documents through OCR extraction, face recognition, and authenticity scoring.

## Features

### Backend (ASP.NET Core 8)
- **JWT Authentication**: Secure token-based authentication with role-based access control
- **Document Storage**: Local file storage with validation and processing
- **OCR Integration**: Tesseract OCR for text extraction from identity documents
- **AI Analysis**: Document authenticity scoring with image quality assessment
- **Face Matching**: ONNX Runtime with ArcFace model for facial recognition
- **Workflow Orchestration**: Automated verification pipeline with manual review capabilities
- **Database**: PostgreSQL with Entity Framework Core

### Frontend (Next.js 16 + TypeScript)
- **Authentication Pages**: Login and user management
- **Document Upload**: Drag-and-drop file upload with validation
- **Verification Dashboard**: Real-time status tracking and metrics
- **Review Interface**: Manual review queue for verification officers
- **Administrative Tools**: User management and system configuration
- **Responsive Design**: Mobile-friendly interface with TailwindCSS

### AI/ML Integration
- **Tesseract OCR**: Multi-language text extraction with confidence scoring
- **ArcFace ONNX Model**: High-accuracy face recognition with 512-dimensional embeddings
- **Image Quality Assessment**: Blur detection, resolution analysis, and compression validation
- **Document Authenticity**: Comprehensive scoring based on multiple factors
- **Tampering Detection**: Error Level Analysis and metadata consistency checks

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+ and npm
- PostgreSQL 15+
- Tesseract OCR 5.x
- ONNX Runtime

### Backend Setup

1. **Navigate to backend directory**:
   ```bash
   cd backend
   ```

2. **Install dependencies**:
   ```bash
   dotnet restore
   ```

3. **Configure database**:
   - Update connection string in `appsettings.json`
   - Run database migrations:
     ```bash
     dotnet ef database update
     ```

4. **Install Tesseract**:
   - Ubuntu/Debian: `sudo apt-get install tesseract-ocr tesseract-ocr-eng libtesseract-dev`
   - macOS: `brew install tesseract tesseract-lang`

5. **Download ONNX model**:
   - Place ArcFace model at `models/arcface_resnet100.onnx`
   - Available from: [ONNX Model Zoo](https://github.com/onnx/models)

6. **Run the backend**:
   ```bash
   dotnet run
   ```

### Frontend Setup

1. **Install dependencies**:
   ```bash
   npm install
   ```

2. **Configure API URL**:
   - Set `NEXT_PUBLIC_API_URL` in `.env.local`:
     ```
     NEXT_PUBLIC_API_URL=http://localhost:5000/api
     ```

3. **Run the frontend**:
   ```bash
   npm run dev
   ```

## Default Credentials

- **Email**: admin@docverify.com
- **Password**: Admin123!

## API Documentation

Once the backend is running, visit:
- Swagger UI: http://localhost:5000/swagger
- Health Check: http://localhost:5000/health

## Architecture

### Backend Services
- **Auth.Api**: Authentication and user management
- **Documents.Api**: File upload and storage
- **Ocr.Api**: Text extraction with Tesseract
- **AI.Analysis.Api**: Document authenticity scoring
- **FaceMatching.Api**: Face recognition with ONNX
- **Workflow.Api**: Verification orchestration

### Database Schema
- **users**: User accounts and roles
- **verifications**: Main verification requests
- **documents**: File storage metadata
- **ocr_results**: OCR extraction data
- **authenticity_scores**: AI analysis results
- **face_match_results**: Face comparison data
- **verification_logs**: Complete audit trail

### Frontend Structure
- **Authentication**: Login and session management
- **Dashboard**: Overview and statistics
- **Verification**: Document upload and tracking
- **Review**: Manual review interface
- **Admin**: User and system management

## Development

### Running Tests
```bash
# Backend
dotnet test

# Frontend
npm run test
```

### Code Quality
```bash
# Backend
dotnet format

# Frontend
npm run lint
```

### Database Migrations
```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

## Deployment

### Docker
```bash
# Build and run
docker-compose up --build
```

### Production
- Configure environment variables
- Set up PostgreSQL database
- Configure SSL certificates
- Set up monitoring and logging

## Security Features

- JWT-based authentication with refresh tokens
- Role-based authorization (Admin, VerificationOfficer)
- File validation and virus scanning
- Input sanitization and SQL injection prevention
- CORS configuration
- Secure file storage with checksums

## Performance Optimization

- Database indexing and query optimization
- Image processing and compression
- Caching strategies
- Background processing for AI operations
- Lazy loading and code splitting

## Monitoring

- Health checks for all services
- Structured logging with Serilog
- Performance metrics
- Error tracking and alerting
- Database query monitoring

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## License

This project is licensed under the MIT License.

## Support

For support and questions, please open an issue on GitHub or contact the development team.
