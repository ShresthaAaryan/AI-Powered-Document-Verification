'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import DashboardLayout from '@/components/layout/dashboard-layout';
import { VerificationDto, VerificationStatus } from '@/types/shared';
import { verificationService } from '@/lib/api/verification-service';
import { Button } from '@/components/ui/button';

export default function ReviewQueue() {
  const router = useRouter();
  const [verifications, setVerifications] = useState<VerificationDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedStatus, setSelectedStatus] = useState<string>('all');

  const loadReviewQueue = async () => {
    try {
      setIsLoading(true);
      const response = await verificationService.getReviewQueue({ page: 1, pageSize: 50 });
      setVerifications(response.verifications);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load review queue');
      setVerifications([]);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadReviewQueue();
  }, []);

  const filteredVerifications = selectedStatus === 'all'
    ? verifications
    : verifications.filter(v => v.status === selectedStatus);

  const getStatusCount = (status: string) => {
    return verifications.filter(v => v.status === status).length;
  };

  const handleReview = (id: string) => {
    router.push(`/review/${id}`);
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'ReviewNeeded':
        return 'text-yellow-600 bg-yellow-100';
      case 'Processing':
        return 'text-blue-600 bg-blue-100';
      case 'Approved':
        return 'text-green-600 bg-green-100';
      case 'Rejected':
        return 'text-red-600 bg-red-100';
      default:
        return 'text-gray-600 bg-gray-100';
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  if (isLoading) {
    return (
      <DashboardLayout>
        <div className="max-w-7xl mx-auto p-6">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-200 rounded w-1/3 mb-8"></div>
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
              {[1, 2, 3, 4].map((i) => (
                <div key={i} className="bg-white p-6 rounded-lg shadow">
                  <div className="h-6 bg-gray-200 rounded w-1/2 mb-2"></div>
                  <div className="h-8 bg-gray-200 rounded w-3/4"></div>
                </div>
              ))}
            </div>
            <div className="bg-white rounded-lg shadow p-6">
              <div className="space-y-4">
                {[1, 2, 3, 4, 5].map((i) => (
                  <div key={i} className="h-16 bg-gray-200 rounded"></div>
                ))}
              </div>
            </div>
          </div>
        </div>
      </DashboardLayout>
    );
  }

  return (
    <DashboardLayout>
      <div className="max-w-7xl mx-auto p-6">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900">Review Queue</h1>
          <p className="mt-2 text-gray-600">
            Review verifications that require manual attention
          </p>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
          <div className="bg-white p-6 rounded-lg shadow border-2 border-yellow-200">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-gray-600">Needs Review</p>
                <p className="text-2xl font-bold text-yellow-600">{getStatusCount('ReviewNeeded')}</p>
              </div>
              <div className="bg-yellow-100 rounded-full p-3">
                <span className="text-yellow-600 text-2xl">👁️</span>
              </div>
            </div>
          </div>

          <div className="bg-white p-6 rounded-lg shadow">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-gray-600">Processing</p>
                <p className="text-2xl font-bold text-blue-600">{getStatusCount('Processing')}</p>
              </div>
              <div className="bg-blue-100 rounded-full p-3">
                <span className="text-blue-600 text-2xl">⏳</span>
              </div>
            </div>
          </div>

          <div className="bg-white p-6 rounded-lg shadow">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-gray-600">Approved Today</p>
                <p className="text-2xl font-bold text-green-600">{getStatusCount('Approved')}</p>
              </div>
              <div className="bg-green-100 rounded-full p-3">
                <span className="text-green-600 text-2xl">✓</span>
              </div>
            </div>
          </div>

          <div className="bg-white p-6 rounded-lg shadow">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm font-medium text-gray-600">Rejected Today</p>
                <p className="text-2xl font-bold text-red-600">{getStatusCount('Rejected')}</p>
              </div>
              <div className="bg-red-100 rounded-full p-3">
                <span className="text-red-600 text-2xl">✗</span>
              </div>
            </div>
          </div>
        </div>

        {/* Status Filter */}
        <div className="bg-white rounded-lg shadow p-4 mb-6">
          <div className="flex flex-wrap gap-2">
            <button
              onClick={() => setSelectedStatus('all')}
              className={`px-4 py-2 rounded-md text-sm font-medium ${
                selectedStatus === 'all'
                  ? 'bg-blue-600 text-white'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
              }`}
            >
              All ({verifications.length})
            </button>
            <button
              onClick={() => setSelectedStatus('ReviewNeeded')}
              className={`px-4 py-2 rounded-md text-sm font-medium ${
                selectedStatus === 'ReviewNeeded'
                  ? 'bg-yellow-600 text-white'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
              }`}
            >
              Needs Review ({getStatusCount('ReviewNeeded')})
            </button>
            <button
              onClick={() => setSelectedStatus('Processing')}
              className={`px-4 py-2 rounded-md text-sm font-medium ${
                selectedStatus === 'Processing'
                  ? 'bg-blue-600 text-white'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
              }`}
            >
              Processing ({getStatusCount('Processing')})
            </button>
            <button
              onClick={() => setSelectedStatus('Approved')}
              className={`px-4 py-2 rounded-md text-sm font-medium ${
                selectedStatus === 'Approved'
                  ? 'bg-green-600 text-white'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
              }`}
            >
              Approved ({getStatusCount('Approved')})
            </button>
            <button
              onClick={() => setSelectedStatus('Rejected')}
              className={`px-4 py-2 rounded-md text-sm font-medium ${
                selectedStatus === 'Rejected'
                  ? 'bg-red-600 text-white'
                  : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
              }`}
            >
              Rejected ({getStatusCount('Rejected')})
            </button>
          </div>
        </div>

        {/* Error State */}
        {error && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-md p-4">
            <div className="text-sm text-red-600">{error}</div>
          </div>
        )}

        {/* Verification List */}
        {filteredVerifications.length > 0 ? (
          <div className="bg-white rounded-lg shadow overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Reference
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Document Type
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Status
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Priority
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Submitted
                    </th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                      Score
                    </th>
                    <th className="relative px-6 py-3">
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {filteredVerifications.map((verification) => (
                    <tr key={verification.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                        {verification.referenceNumber}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                        {verification.documentType}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${getStatusColor(verification.status)}`}>
                          {verification.status}
                        </span>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                        <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
                          verification.priority === 'Urgent' ? 'bg-red-100 text-red-800' :
                          verification.priority === 'High' ? 'bg-orange-100 text-orange-800' :
                          verification.priority === 'Low' ? 'bg-gray-100 text-gray-800' :
                          'bg-blue-100 text-blue-800'
                        }`}>
                          {verification.priority}
                        </span>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                        {formatDate(verification.createdAt)}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                        {verification.authenticityScore ? (
                          <div className="flex items-center">
                            <span className="mr-2">{verification.authenticityScore.overallScore}/100</span>
                            <span className={`text-xs px-2 py-1 rounded-full ${
                              verification.authenticityScore.classification === 'Genuine' ? 'text-green-600 bg-green-100' :
                              verification.authenticityScore.classification === 'Suspicious' ? 'text-yellow-600 bg-yellow-100' :
                              'text-red-600 bg-red-100'
                            }`}>
                              {verification.authenticityScore.classification}
                            </span>
                          </div>
                        ) : (
                          <span className="text-gray-400">-</span>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                        <Button
                          onClick={() => handleReview(verification.id)}
                          variant="primary"
                          size="sm"
                        >
                          Review
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        ) : (
          <div className="bg-white rounded-lg shadow p-12 text-center">
            <div className="text-gray-400 text-4xl mb-4">📋</div>
            <h3 className="text-lg font-medium text-gray-900 mb-2">No verifications to review</h3>
            <p className="text-gray-600 mb-4">
              {selectedStatus === 'all'
                ? 'All verifications are up to date'
                : `No verifications with status: ${selectedStatus}`}
            </p>
            <Button
              onClick={() => router.push('/dashboard')}
              variant="outline"
            >
              Return to Dashboard
            </Button>
          </div>
        )}

        {/* Refresh Button */}
        <div className="mt-6 text-center">
          <Button
            onClick={loadReviewQueue}
            variant="outline"
            disabled={isLoading}
          >
            Refresh Queue
          </Button>
        </div>
      </div>
    </DashboardLayout>
  );
}