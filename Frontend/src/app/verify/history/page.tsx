'use client';

import { useEffect, useMemo, useState } from 'react';
import { useRouter } from 'next/navigation';
import DashboardLayout from '@/components/layout/dashboard-layout';
import VerificationTable from '@/components/verification/history/verification-table';
import VerificationFiltersComponent from '@/components/verification/history/verification-filters';
import {
  INITIAL_VERIFICATION_FILTERS,
  type VerificationFilters
} from '@/components/verification/history/verification-filters';
import { VerificationDto } from '@/types/shared';
import { verificationService } from '@/lib/api/verification-service';

export default function VerificationHistoryPage() {
  const router = useRouter();
  const [allVerifications, setAllVerifications] = useState<VerificationDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; message: string } | null>(null);
  const [deletingVerificationIds, setDeletingVerificationIds] = useState<string[]>([]);
  const [filters, setFilters] = useState<VerificationFilters>(INITIAL_VERIFICATION_FILTERS);
  const [pagination, setPagination] = useState({
    page: 1,
    pageSize: 20,
    totalCount: 0,
  });

  const loadVerifications = async () => {
    try {
      setIsLoading(true);
      const response = await verificationService.getMyVerifications({ page: 1, pageSize: 1000 });
      setAllVerifications(response.verifications);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load verifications');
      setAllVerifications([]);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadVerifications();
  }, []);

  const filteredVerifications = useMemo(() => {
    return allVerifications.filter((verification) => {
      const searchTerm = filters.searchTerm.trim().toLowerCase();
      const matchesSearch =
        !searchTerm ||
        verification.referenceNumber.toLowerCase().includes(searchTerm) ||
        (verification.submittedBy || '').toLowerCase().includes(searchTerm) ||
        (verification.assignedTo || '').toLowerCase().includes(searchTerm);

      const matchesStatus =
        filters.status.length === 0 || filters.status.includes(verification.status);
      const matchesDocumentType =
        filters.documentType.length === 0 || filters.documentType.includes(verification.documentType);
      const matchesPriority =
        filters.priority.length === 0 || filters.priority.includes(verification.priority);

      const createdAt = new Date(verification.createdAt);
      const startDate = filters.dateRange?.startDate ? new Date(filters.dateRange.startDate) : null;
      const endDate = filters.dateRange?.endDate ? new Date(filters.dateRange.endDate) : null;

      if (endDate) {
        endDate.setHours(23, 59, 59, 999);
      }

      const matchesStartDate = !startDate || createdAt >= startDate;
      const matchesEndDate = !endDate || createdAt <= endDate;

      return (
        matchesSearch &&
        matchesStatus &&
        matchesDocumentType &&
        matchesPriority &&
        matchesStartDate &&
        matchesEndDate
      );
    });
  }, [allVerifications, filters]);

  useEffect(() => {
    setPagination((prev) => ({ ...prev, totalCount: filteredVerifications.length }));
  }, [filteredVerifications.length]);

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(filteredVerifications.length / pagination.pageSize));
    if (pagination.page > maxPage) {
      setPagination((prev) => ({ ...prev, page: maxPage }));
    }
  }, [filteredVerifications.length, pagination.page, pagination.pageSize]);

  const verifications = useMemo(() => {
    const startIndex = (pagination.page - 1) * pagination.pageSize;
    return filteredVerifications.slice(startIndex, startIndex + pagination.pageSize);
  }, [filteredVerifications, pagination.page, pagination.pageSize]);

  const handleFiltersChange = (filters: VerificationFilters) => {
    setFilters(filters);
    setPagination(prev => ({ ...prev, page: 1 }));
  };

  const handleReset = () => {
    setFilters(INITIAL_VERIFICATION_FILTERS);
    setPagination(prev => ({ ...prev, page: 1 }));
  };

  const handleViewDetails = (id: string) => {
    router.push(`/verify/${id}/status`);
  };

  const handleDelete = async (verificationToDelete: VerificationDto) => {
    const { id, referenceNumber } = verificationToDelete;
    const shouldDelete = confirm(
      `Delete verification "${referenceNumber}"?\n\nThis action cannot be undone.`
    );
    if (!shouldDelete) {
      return;
    }

    const previousVerifications = allVerifications;
    setNotice(null);
    setDeletingVerificationIds((prev) => [...prev, id]);
    setAllVerifications((prev) => prev.filter((verification) => verification.id !== id));

    try {
      await verificationService.deleteVerification(id);
      await loadVerifications();
      setNotice({
        type: 'success',
        message: `Verification ${referenceNumber} deleted successfully.`,
      });
    } catch (err) {
      setAllVerifications(previousVerifications);
      setNotice({
        type: 'error',
        message: err instanceof Error ? err.message : 'Failed to delete verification',
      });
    } finally {
      setDeletingVerificationIds((prev) => prev.filter((verificationId) => verificationId !== id));
    }
  };

  const handlePageChange = (newPage: number) => {
    setPagination(prev => ({ ...prev, page: newPage }));
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
          filters={filters}
          onFiltersChange={handleFiltersChange}
          onReset={handleReset}
        />

        {/* Error State */}
        {error && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-md p-4">
            <div className="text-sm text-red-600">{error}</div>
          </div>
        )}
        {notice && (
          <div
            className={`mb-6 border rounded-md p-4 ${
              notice.type === 'success'
                ? 'bg-green-50 border-green-200 text-green-700'
                : 'bg-red-50 border-red-200 text-red-600'
            }`}
          >
            <div className="flex items-center justify-between gap-4">
              <span className="text-sm">{notice.message}</span>
              <button
                type="button"
                onClick={() => setNotice(null)}
                className="text-xs font-medium hover:opacity-80"
              >
                Dismiss
              </button>
            </div>
          </div>
        )}

        {/* Table */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200">
          <VerificationTable
            verifications={verifications}
            onViewDetails={handleViewDetails}
            onDelete={handleDelete}
            deletingVerificationIds={deletingVerificationIds}
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
                  let pageNumber: number;
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
      </div>
    </DashboardLayout>
  );
}