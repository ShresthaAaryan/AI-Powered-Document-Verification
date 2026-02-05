import { apiClient } from './api-client';
import { authService } from '@/lib/auth/auth-service';
import {
  VerificationDto,
  CreateVerificationRequest,
  UpdateStatusRequest,
  VerificationListRequest,
  VerificationListResponse,
  WorkflowStatsDto,
  UpdateStageRequest,
  AssignOfficerRequest
} from '@/types/shared';

class VerificationService {
  async createVerification(request: CreateVerificationRequest): Promise<VerificationDto> {
    const formData = new FormData();

    // Add form fields
    formData.append('DocumentType', request.documentType);
    if (request.applicantName) formData.append('ApplicantName', request.applicantName);
    if (request.dateOfBirth) formData.append('DateOfBirth', request.dateOfBirth);
    if (request.referenceNumber) formData.append('ReferenceNumber', request.referenceNumber);
    if (request.priority) formData.append('Priority', request.priority);

    // Add files
    formData.append('IdDocument', request.idDocument);
    formData.append('SelfieImage', request.selfieImage);
    if (request.documentType === 'CitizenshipCard' && request.idDocumentBack) {
      formData.append('IdDocumentBack', request.idDocumentBack);
    }

    const response = await fetch(`${apiClient['baseUrl']}/verification`, {
      method: 'POST',
      headers: authService.getAuthHeader(),
      body: formData,
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
    }

    return response.json();
  }

  async getVerification(id: string): Promise<VerificationDto> {
    return apiClient.get<VerificationDto>(`/verification/${id}`);
  }

  async getMyVerifications(request: VerificationListRequest = {}): Promise<VerificationListResponse> {
    const params = {
      page: request.page || 1,
      pageSize: request.pageSize || 20,
    };

    const response = await apiClient.get<VerificationDto[]>('/verification/my-verifications', params);

    // Transform response to match expected format
    return {
      verifications: response,
      totalCount: response.length, // This would ideally come from the API
      currentPage: request.page || 1,
      pageSize: request.pageSize || 20,
      totalPages: Math.ceil(response.length / (request.pageSize || 20)),
      hasNextPage: false,
      hasPreviousPage: false,
    };
  }

  async getAllVerifications(request: VerificationListRequest = {}): Promise<VerificationListResponse> {
    const params = {
      page: request.page || 1,
      pageSize: request.pageSize || 20,
      status: request.status,
      documentType: request.documentType,
      priority: request.priority,
      startDate: request.startDate,
      endDate: request.endDate,
      searchTerm: request.searchTerm,
    };

    const response = await apiClient.get<VerificationDto[]>('/verification', params);

    return {
      verifications: response,
      totalCount: response.length,
      currentPage: request.page || 1,
      pageSize: request.pageSize || 20,
      totalPages: Math.ceil(response.length / (request.pageSize || 20)),
      hasNextPage: false,
      hasPreviousPage: false,
    };
  }

  async updateVerificationStatus(id: string, request: UpdateStatusRequest): Promise<VerificationDto> {
    return apiClient.put<VerificationDto>(`/verification/${id}/status`, request);
  }

  async deleteVerification(id: string): Promise<void> {
    return apiClient.delete<void>(`/verification/${id}`);
  }

  async downloadDocument(verificationId: string, documentId: string, filename?: string): Promise<void> {
    return apiClient.downloadFile(`/verification/${verificationId}/document/${documentId}`, filename);
  }

  // Workflow methods
  async startVerification(id: string): Promise<VerificationDto> {
    return apiClient.post<VerificationDto>(`/workflow/${id}/start`);
  }

  async processVerification(id: string): Promise<VerificationDto> {
    return apiClient.post<VerificationDto>(`/workflow/${id}/process`);
  }

  async updateVerificationStage(id: string, request: UpdateStageRequest): Promise<VerificationDto> {
    return apiClient.post<VerificationDto>(`/workflow/${id}/stage`, request);
  }

  async makeFinalDecision(id: string): Promise<VerificationDto> {
    return apiClient.post<VerificationDto>(`/workflow/${id}/decision`);
  }

  async needsManualReview(id: string): Promise<boolean> {
    return apiClient.get<boolean>(`/workflow/${id}/needs-review`);
  }

  async assignToOfficer(id: string, request: AssignOfficerRequest): Promise<VerificationDto> {
    return apiClient.post<VerificationDto>(`/workflow/${id}/assign`, request);
  }

  async getReviewQueue(request: VerificationListRequest = {}): Promise<VerificationListResponse> {
    const params = {
      page: request.page || 1,
      pageSize: request.pageSize || 20,
      priority: request.priority,
    };

    const response = await apiClient.get<VerificationDto[]>('/workflow/queue', params);

    return {
      verifications: response,
      totalCount: response.length,
      currentPage: request.page || 1,
      pageSize: request.pageSize || 20,
      totalPages: Math.ceil(response.length / (request.pageSize || 20)),
      hasNextPage: false,
      hasPreviousPage: false,
    };
  }

  async getWorkflowStats(): Promise<WorkflowStatsDto> {
    return apiClient.get<WorkflowStatsDto>('/workflow/stats');
  }

  async deleteVerification(id: string): Promise<void> {
    await apiClient.delete(`/verification/${id}`);
  }

  // Helper method to get verification status color
  getStatusColor(status: string): string {
    switch (status) {
      case 'Approved':
        return 'text-green-600 bg-green-100';
      case 'Rejected':
        return 'text-red-600 bg-red-100';
      case 'Processing':
        return 'text-blue-600 bg-blue-100';
      case 'ReviewNeeded':
        return 'text-yellow-600 bg-yellow-100';
      case 'Pending':
        return 'text-gray-600 bg-gray-100';
      default:
        return 'text-gray-600 bg-gray-100';
    }
  }

  // Helper method to get status icon
  getStatusIcon(status: string): string {
    switch (status) {
      case 'Approved':
        return '✓';
      case 'Rejected':
        return '✗';
      case 'Processing':
        return '⏳';
      case 'ReviewNeeded':
        return '⚠';
      case 'Pending':
        return '📋';
      default:
        return '❓';
    }
  }

  // Helper method to format processing time
  formatProcessingTime(createdAt: string, completedAt?: string): string {
    const start = new Date(createdAt);
    const end = completedAt ? new Date(completedAt) : new Date();
    const diffMs = end.getTime() - start.getTime();

    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);

    if (diffHours > 0) {
      return `${diffHours}h ${diffMins % 60}m`;
    }
    return `${diffMins}m`;
  }
}

export const verificationService = new VerificationService();