'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import DashboardLayout from '@/components/layout/dashboard-layout';
import VerificationTable from '@/components/verification/history/verification-table';
import VerificationFiltersComponent from '@/components/verification/history/verification-filters';
import type { VerificationFilters } from '@/components/verification/history/verification-filters';
import { VerificationDto, VerificationListRequest } from '@/types/shared';
import { verificationService } from '@/lib/api/verification-service';

export default function VerificationHistoryPage() {
  const router = useRouter();
  const [verifications, setVerifications] = useState<VerificationDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: 20,
    totalCount: 0,
  });

  const loadVerifications = async (request: VerificationListRequest = {}) => {
    try {
      setIsLoading(true);
      const response = await verificationService.getMyVerifications({
        ...request,
        page: pagination.page,
        pageSize: pagination.pageSize,
      });

      setVerifications(response.verifications);
      setPagination(prev => ({
        ...prev,
        totalCount: response.totalCount,
      }));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load verifications');
      setVerifications([]);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadVerifications();
  }, []);

  const handleFiltersChange = (filters: VerificationFilters) => {
    const request: VerificationListRequest = {
      page: 1, // Reset to first page when filters change
      pageSize: pagination.pageSize,
      searchTerm: filters.searchTerm,
      status: filters.status.length > 0 ? filters.status[0] as any : undefined,
      documentType: filters.documentType.length > 0 ? filters.documentType[0] as any : undefined,
      priority: filters.priority.length > 0 ? filters.priority[0] as any : undefined,
      startDate: filters.dateRange?.startDate,
      endDate: filters.dateRange?.endDate,
    };

    setPagination(prev => ({ ...prev, page: 1 }));
    loadVerifications(request);
  };

  const handleReset = () => {
    setPagination(prev => ({ ...prev, page: 1 }));
    loadVerifications();
  };

  const handleViewDetails = (id: string) => {
    router.push(`/verify/${id}/status`);
  };

  const handleDelete = async (id: string) => {
    if (!confirm('Are you sure you want to delete this verification? This action cannot be undone.')) {
      return;
    }

    try {
      await verificationService.deleteVerification(id);
      // Reload verifications after deletion
      await loadVerifications();
    } catch (err) {
      alert(err instanceof Error ? err.message : 'Failed to delete verification');
    }
  };

  const handlePageChange = (newPage: number) => {
    setPagination(prev => ({ ...prev, page: newPage }));
    loadVerifications({ page: newPage });
  };

  const totalPages = Math.ceil(pagination.totalCount / pagination.pageSize);

  return (
    <DashboardLayout>
      <div className="max-w-7xl mx-auto p-6">
        {/* Header */}
        <div className="mb-8">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-gray-900">Verification History</h1>
              <p className="mt-2 text-gray-600">
                View and search your verification records
              </p>
            </div>
            <div className="flex space-x-4">
              <button
                onClick={() => router.push('/verify/new')}
                className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
              >
                New Verification
              </button>
            </div>
          </div>
        </div>

        {/* Filters */}
        <VerificationFiltersComponent
          onFiltersChange={handleFiltersChange}
          onReset={handleReset}
        />

        {/* Error State */}
        {error && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-md p-4">
            <div className="text-sm text-red-600">{error}</div>
          </div>
        )}

        {/* Table */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200">
          <VerificationTable
            verifications={verifications}
            onViewDetails={handleViewDetails}
            onDelete={handleDelete}
          />
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="mt-6 flex items-center justify-between">
            <div className="text-sm text-gray-700">
              Showing {((pagination.page - 1) * pagination.pageSize) + 1} to{' '}
              {Math.min(pagination.page * pagination.pageSize, pagination.totalCount)} of{' '}
              {pagination.totalCount} results
            </div>
            <div className="flex items-center space-x-2">
              <button
                onClick={() => handlePageChange(pagination.page - 1)}
                disabled={pagination.page === 1}
                className="px-3 py-1 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Previous
              </button>

              <div className="flex items-center space-x-1">
                {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                  let pageNumber;
                  if (totalPages <= 5) {
                    pageNumber = i + 1;
                  } else if (pagination.page <= 3) {
                    pageNumber = i + 1;
                  } else if (pagination.page >= totalPages - 2) {
                    pageNumber = totalPages - 4 + i;
                  } else {
                    pageNumber = pagination.page - 2 + i;
                  }

                  return (
                    <button
                      key={pageNumber}
                      onClick={() => handlePageChange(pageNumber)}
                      className={`px-3 py-1 text-sm font-medium rounded-md ${
                        pageNumber === pagination.page
                          ? 'bg-blue-600 text-white'
                          : 'border border-gray-300 text-gray-700 hover:bg-gray-50'
                      }`}
                    >
                      {pageNumber}
                    </button>
                  );
                })}
              </div>

              <button
                onClick={() => handlePageChange(pagination.page + 1)}
                disabled={pagination.page === totalPages}
                className="px-3 py-1 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                Next
              </button>
            </div>
          </div>
        )}

        {/* Empty State */}
        {!isLoading && verifications.length === 0 && !error && (
          <div className="text-center py-12">
            <div className="text-gray-400 text-4xl mb-4">📋</div>
            <h3 className="text-lg font-medium text-gray-900 mb-2">No verifications found</h3>
            <p className="text-gray-600 mb-4">Get started by creating your first verification</p>
            <button
              onClick={() => router.push('/verify/new')}
              className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700"
            >
              Create Verification
            </button>
          </div>
        )}
      </div>
    </DashboardLayout>
  );
}