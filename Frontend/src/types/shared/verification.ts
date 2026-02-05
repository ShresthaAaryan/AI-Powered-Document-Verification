export interface CreateVerificationRequest {
  documentType: DocumentType;
  idDocument: File;
  selfieImage: File;
  /** For CitizenshipCard only: optional back side (data in English). */
  idDocumentBack?: File;
  applicantName?: string;
  dateOfBirth?: string;
  referenceNumber?: string;
  priority?: Priority;
}

export interface VerificationDto {
  id: string;
  referenceNumber: string;
  documentType: DocumentType;
  status: VerificationStatus;
  priority: Priority;
  finalDecision?: FinalDecision;
  decisionReason?: string;
  errorMessage?: string;
  userActionRequired?: string;
  processingStartedAt?: string;
  completedAt?: string;
  createdAt: string;
  updatedAt: string;
  submittedAt: string;

  // Related data
  ocrResult?: OcrResultDto;
  authenticityScore?: AuthenticityScoreDto;
  faceMatchResult?: FaceMatchResultDto;
  documents: DocumentDto[];

  // User information
  submittedBy?: string;
  assignedTo?: string;
}

export interface OcrResultDto {
  id: string;
  rawText?: string;
  confidenceScore?: number;
  processingTimeMs?: number;
  languageDetected?: string;
  extractedFields?: Record<string, ExtractedFieldDto>;
}

export interface ExtractedFieldDto {
  value?: string;
  confidence: number;
  boundingBox?: BoundingBoxDto;
  format?: string;
}

export interface BoundingBoxDto {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface AuthenticityScoreDto {
  id: string;
  overallScore: number;
  classification: AuthenticityClassification;
  fieldCompletenessScore?: number;
  formatConsistencyScore?: number;
  imageQualityScore?: number;
  securityFeaturesScore?: number;
  metadataConsistencyScore?: number;
  detailedAnalysis?: any;
  processingTimeMs?: number;
  modelVersion?: string;
}

export interface FaceMatchResultDto {
  id: string;
  idFaceDetected: boolean;
  selfieFaceDetected: boolean;
  similarityScore?: number;
  matchDecision?: boolean;
  confidenceThreshold?: number;
  faceDetectionDetails?: any;
  processingTimeMs?: number;
  modelVersion?: string;
}

export interface DocumentDto {
  id: string;
  documentType: DocumentType;
  fileName: string;
  fileSizeBytes: number;
  mimeType: string;
  originalFileName?: string;
  uploadedAt: string;
  isPrimary: boolean;
}

export interface VerificationListRequest {
  page?: number;
  pageSize?: number;
  status?: VerificationStatus;
  documentType?: DocumentType;
  priority?: Priority;
  startDate?: string;
  endDate?: string;
  searchTerm?: string;
}

export interface VerificationListResponse {
  verifications: VerificationDto[];
  totalCount: number;
  currentPage: number;
  pageSize: number;
  totalPages: number;
}

export interface UpdateStatusRequest {
  status: VerificationStatus;
  reason?: string;
}

export interface UpdateStageRequest {
  stage: string;
  status: string;
}

export interface AssignOfficerRequest {
  officerId: string;
}

export interface WorkflowStatsDto {
  totalVerifications: number;
  pendingVerifications: number;
  processingVerifications: number;
  completedVerifications: number;
  approvedVerifications: number;
  rejectedVerifications: number;
  reviewNeededVerifications: number;
  averageProcessingTimeMinutes: number;
  todayVerifications: number;
}

export interface VerificationLogEntry {
  id: string;
  verificationId: string;
  userId?: string;
  action: string;
  serviceName?: string;
  previousStatus?: string;
  newStatus?: string;
  details?: any;
  ipAddress?: string;
  userAgent?: string;
  processingTimeMs?: number;
  errorMessage?: string;
  createdAt: string;
}

// Enums
export type DocumentType = 'Passport' | 'DriversLicense' | 'NationalID' | 'CitizenshipCard';
export type VerificationStatus = 'Pending' | 'Processing' | 'Approved' | 'Rejected' | 'ReviewNeeded';
export type FinalDecision = 'Approved' | 'Rejected' | 'RequiresManualReview';
export type Priority = 'Low' | 'Normal' | 'High' | 'Urgent';
export type AuthenticityClassification = 'Genuine' | 'Suspicious' | 'Invalid';
export type DocumentFileType = 'IDDocument' | 'Selfie' | 'SupportingDocument';