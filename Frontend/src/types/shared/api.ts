// Generic API response types
export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  currentPage: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface ApiError {
  error: string;
  details?: string;
  code?: string;
  timestamp: string;
}

export interface ValidationError {
  field: string;
  message: string;
}

export interface FileUploadResponse {
  fileName: string;
  originalFileName: string;
  fileSize: number;
  mimeType: string;
  uploadPath: string;
  uploadedAt: string;
}

export interface HealthCheckResponse {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  checks: {
    database: HealthCheckResult;
    aiModels: HealthCheckResult;
    fileStorage: HealthCheckResult;
  };
  overallStatus: string;
  timestamp: string;
}

export interface HealthCheckResult {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  responseTime?: number;
  details?: string;
}

// Search and filter types
export interface SearchFilters {
  searchTerm?: string;
  dateRange?: {
    startDate: string;
    endDate: string;
  };
  status?: string[];
  documentType?: string[];
  priority?: string[];
}

export interface SortOptions {
  field: string;
  direction: 'asc' | 'desc';
}

export interface PaginationOptions {
  page: number;
  pageSize: number;
}

// WebSocket message types
export interface WebSocketMessage {
  type: string;
  data: any;
  timestamp: string;
  id: string;
}

export interface VerificationUpdateMessage extends WebSocketMessage {
  type: 'verification_update';
  data: {
    verificationId: string;
    status: string;
    stage?: string;
    progress?: number;
  };
}

export interface NotificationMessage extends WebSocketMessage {
  type: 'notification';
  data: {
    title: string;
    message: string;
    severity: 'info' | 'warning' | 'error' | 'success';
    verificationId?: string;
  };
}

// Export/Import types
export interface ExportOptions {
  format: 'csv' | 'pdf' | 'excel';
  fields: string[];
  filters?: SearchFilters;
  dateRange?: {
    startDate: string;
    endDate: string;
  };
}

export interface BulkActionRequest {
  verificationIds: string[];
  action: 'approve' | 'reject' | 'assign' | 'delete';
  parameters?: Record<string, any>;
}

// System configuration types
export interface SystemSettings {
  maxFileSize: number;
  allowedFileTypes: string[];
  faceMatchingThreshold: number;
  authenticityThresholds: {
    genuine: number;
    suspicious: number;
  };
  ocrSettings: {
    languages: string[];
    confidenceThreshold: number;
  };
  notificationSettings: {
    emailEnabled: boolean;
    webhookUrl?: string;
  };
}