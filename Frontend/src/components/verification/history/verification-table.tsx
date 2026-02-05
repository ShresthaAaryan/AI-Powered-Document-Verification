'use client';

import { useState } from 'react';
import { VerificationDto, VerificationStatus, DocumentType } from '@/types/shared';
import { verificationService } from '@/lib/api/verification-service';

interface VerificationTableProps {
  verifications: VerificationDto[];
  onViewDetails: (id: string) => void;
  onDelete?: (id: string) => void;
}

export default function VerificationTable({ verifications, onViewDetails, onDelete }: VerificationTableProps) {
  const [sortField, setSortField] = useState<keyof VerificationDto>('createdAt');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc'>('desc');

  const handleSort = (field: keyof VerificationDto) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('desc');
    }
  };

  const sortedVerifications = [...verifications].sort((a, b) => {
    const aValue = a[sortField];
    const bValue = b[sortField];

    let comparison = 0;
    if (aValue < bValue) comparison = -1;
    if (aValue > bValue) comparison = 1;

    return sortDirection === 'asc' ? comparison : -comparison;
  });

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatProcessingTime = (verification: VerificationDto) => {
    if (!verification.processingStartedAt || !verification.completedAt) {
      return '-';
    }

    const start = new Date(verification.processingStartedAt);
    const end = new Date(verification.completedAt);
    const diffMs = end.getTime() - start.getTime();
    const diffMins = Math.floor(diffMs / 60000);

    if (diffMins < 60) {
      return `${diffMins}m`;
    }

    const hours = Math.floor(diffMins / 60);
    const mins = diffMins % 60;
    return `${hours}h ${mins}m`;
  };

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50">
          <tr>
            <th
              scope="col"
              className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
              onClick={() => handleSort('referenceNumber')}
            >
              <div className="flex items-center">
                Reference Number
                {sortField === 'referenceNumber' && (
                  <span className="ml-1">
                    {sortDirection === 'asc' ? '↑' : '↓'}
                  </span>
                )}
              </div>
            </th>
            <th
              scope="col"
              className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
              onClick={() => handleSort('documentType')}
            >
              <div className="flex items-center">
                Document Type
                {sortField === 'documentType' && (
                  <span className="ml-1">
                    {sortDirection === 'asc' ? '↑' : '↓'}
                  </span>
                )}
              </div>
            </th>
            <th
              scope="col"
              className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
              onClick={() => handleSort('status')}
            >
              <div className="flex items-center">
                Status
                {sortField === 'status' && (
                  <span className="ml-1">
                    {sortDirection === 'asc' ? '↑' : '↓'}
                  </span>
                )}
              </div>
            </th>
            <th
              scope="col"
              className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
              onClick={() => handleSort('createdAt')}
            >
              <div className="flex items-center">
                Submitted
                {sortField === 'createdAt' && (
                  <span className="ml-1">
                    {sortDirection === 'asc' ? '↑' : '↓'}
                  </span>
                )}
              </div>
            </th>
            <th
              scope="col"
              className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
            >
              Processing Time
            </th>
            <th
              scope="col"
              className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
            >
              Authenticity
            </th>
            <th
              scope="col"
              className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider"
            >
              Face Match
            </th>
            <th
              scope="col"
              className="relative px-6 py-3">
                <span className="sr-only">Actions</span>
              </th>
          </tr>
        </thead>
        <tbody className="bg-white divide-y divide-gray-200">
          {sortedVerifications.length > 0 ? (
            sortedVerifications.map((verification) => (
              <tr key={verification.id} className="hover:bg-gray-50">
                <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">
                  {verification.referenceNumber}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                  {verification.documentType}
                </td>
                <td className="px-6 py-4 whitespace-nowrap">
                  <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${verificationService.getStatusColor(verification.status)}`}>
                    {verificationService.getStatusIcon(verification.status)} {verification.status}
                  </span>
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                  {formatDate(verification.createdAt)}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                  {formatProcessingTime(verification)}
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
                <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                  {verification.faceMatchResult?.similarityScore ? (
                    <div className="flex items-center">
                      <span className="mr-2">
                        {Math.round(Number(verification.faceMatchResult.similarityScore) * 100)}%
                      </span>
                      <span className={`text-xs px-2 py-1 rounded-full ${
                        verification.faceMatchResult.matchDecision
                          ? 'text-green-600 bg-green-100'
                          : 'text-red-600 bg-red-100'
                      }`}>
                        {verification.faceMatchResult.matchDecision ? 'Match' : 'No Match'}
                      </span>
                    </div>
                  ) : (
                    <span className="text-gray-400">-</span>
                  )}
                </td>
                <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                  <button
                    onClick={() => onViewDetails(verification.id)}
                    className="text-blue-600 hover:text-blue-900 mr-3"
                  >
                    View
                  </button>
                  <button
                    onClick={() => window.open(`/verify/${verification.id}/status`, '_blank')}
                    className="text-gray-600 hover:text-gray-900 mr-3"
                  >
                    Status
                  </button>
                  {onDelete && (
                    <button
                      onClick={() => onDelete(verification.id)}
                      className="text-red-600 hover:text-red-900"
                      title="Delete verification"
                    >
                      Delete
                    </button>
                  )}
                </td>
              </tr>
            ))
          ) : (
            <tr>
              <td colSpan={8} className="px-6 py-12 text-center">
                <div className="text-gray-400 text-4xl mb-4">📄</div>
                <p className="text-gray-600">No verifications found</p>
                <p className="text-gray-500 text-sm mt-1">Start by creating your first verification</p>
                <a
                  href="/verify/new"
                  className="mt-4 inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700"
                >
                  Create Verification
                </a>
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}