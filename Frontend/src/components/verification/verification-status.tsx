'use client';

import { useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import DashboardLayout from '@/components/layout/dashboard-layout';
import { VerificationDto, VerificationStatus } from '@/types/shared';
import { verificationService } from '@/lib/api/verification-service';

interface Stage {
  id: string;
  name: string;
  status: 'pending' | 'processing' | 'completed' | 'error';
  description: string;
}

export default function VerificationStatus() {
  const params = useParams();
  const router = useRouter();
  const verificationId = params.id as string;

  const [verification, setVerification] = useState<VerificationDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isStarting, setIsStarting] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);

  const stages: Stage[] = [
    {
      id: 'upload',
      name: 'Document Upload',
      status: 'completed',
      description: 'ID document and selfie uploaded successfully',
    },
    {
      id: 'ocr',
      name: 'OCR Processing',
      status: verification?.ocrResult ? 'completed' : verification?.status === 'Processing' ? 'processing' : 'pending',
      description: 'Extracting text from documents using AI',
    },
    {
      id: 'analysis',
      name: 'Authenticity Analysis',
      status: verification?.authenticityScore ? 'completed' : verification?.status === 'Processing' ? 'processing' : 'pending',
      description: 'Analyzing document authenticity and quality',
    },
    {
      id: 'facematch',
      name: 'Face Matching',
      status: verification?.faceMatchResult ? 'completed' : verification?.status === 'Processing' ? 'processing' : 'pending',
      description: 'Comparing ID photo with selfie using AI',
    },
    {
      id: 'decision',
      name: 'Final Decision',
      status: verification?.finalDecision ? 'completed' : verification?.status === 'Processing' ? 'processing' : 'pending',
      description: 'Generating final verification result',
    },
  ];

  const loadVerification = async () => {
    try {
      const data = await verificationService.getVerification(verificationId);
      setVerification(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load verification');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (verificationId) {
      loadVerification();

      // Set up polling for real-time updates (only if not completed)
      const interval = setInterval(() => {
        if (verification?.status !== 'Approved' && verification?.status !== 'Rejected') {
          loadVerification();
        }
      }, 5000);
      return () => clearInterval(interval);
    }
  }, [verificationId, verification?.status]);

  const handleStartProcessing = async () => {
    if (!verification) return;
    
    setIsStarting(true);
    try {
      await verificationService.startVerification(verification.id);
      // Reload verification to get updated status
      await loadVerification();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to start processing');
    } finally {
      setIsStarting(false);
    }
  };

  const handleDelete = async () => {
    if (!verification) return;
    
    setIsDeleting(true);
    try {
      await verificationService.deleteVerification(verification.id);
      // Redirect to dashboard after successful deletion
      router.push('/dashboard');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete verification');
      setIsDeleting(false);
      setShowDeleteConfirm(false);
    }
  };

  const getStageStatus = (stage: Stage): 'pending' | 'processing' | 'completed' | 'error' => {
    if (verification?.status === 'Rejected' || verification?.status === 'Approved') {
      if (stage.id === 'decision') return verification.status === 'Rejected' ? 'error' : 'completed';
    }

    return stage.status;
  };

  const getStageIcon = (status: string) => {
    switch (status) {
      case 'completed':
        return (
          <div className="flex items-center justify-center w-8 h-8 bg-green-100 rounded-full">
            <svg className="w-5 h-5 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5 13l4 4L19 7" />
            </svg>
          </div>
        );
      case 'processing':
        return (
          <div className="flex items-center justify-center w-8 h-8 bg-blue-100 rounded-full">
            <div className="w-5 h-5 border-2 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
          </div>
        );
      case 'error':
        return (
          <div className="flex items-center justify-center w-8 h-8 bg-red-100 rounded-full">
            <svg className="w-5 h-5 text-red-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </div>
        );
      default:
        return (
          <div className="flex items-center justify-center w-8 h-8 bg-gray-100 rounded-full">
            <div className="w-2 h-2 bg-gray-400 rounded-full"></div>
          </div>
        );
    }
  };

  const getStatusColor = (status: VerificationStatus) => {
    switch (status) {
      case 'Approved':
        return 'text-green-600 bg-green-100';
      case 'Rejected':
        return 'text-red-600 bg-red-100';
      case 'Processing':
        return 'text-blue-600 bg-blue-100';
      case 'ReviewNeeded':
        return 'text-yellow-600 bg-yellow-100';
      default:
        return 'text-gray-600 bg-gray-100';
    }
  };

  if (isLoading) {
    return (
      <DashboardLayout>
        <div className="max-w-4xl mx-auto p-6">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-200 rounded w-1/3 mb-8"></div>
            <div className="space-y-6">
              {[1, 2, 3, 4, 5].map((i) => (
                <div key={i} className="flex items-center space-x-4">
                  <div className="w-8 h-8 bg-gray-200 rounded-full"></div>
                  <div className="flex-1">
                    <div className="h-4 bg-gray-200 rounded w-1/4 mb-2"></div>
                    <div className="h-3 bg-gray-200 rounded w-1/2"></div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </DashboardLayout>
    );
  }

  if (error || !verification) {
    return (
      <DashboardLayout>
        <div className="max-w-4xl mx-auto p-6">
          <div className="text-center">
            <div className="text-red-600 text-4xl mb-4">❌</div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">Error Loading Verification</h2>
            <p className="text-gray-600 mb-6">{error || 'Verification not found'}</p>
            <button
              onClick={() => router.push('/dashboard')}
              className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
            >
              Return to Dashboard
            </button>
          </div>
        </div>
      </DashboardLayout>
    );
  }

  return (
    <DashboardLayout>
      <div className="max-w-4xl mx-auto p-6">
        {/* Header */}
        <div className="mb-8">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-gray-900">Verification Status</h1>
              <p className="mt-2 text-gray-600">
                Reference: <span className="font-medium">{verification.referenceNumber}</span>
              </p>
            </div>
            <div className="flex items-center space-x-3">
              {(verification.status === 'Pending' || (verification.errorMessage && verification.errorMessage.trim() !== '')) && (
                <button
                  onClick={handleStartProcessing}
                  disabled={isStarting}
                  className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed text-sm font-medium"
                >
                  {isStarting ? 'Starting...' : (verification.errorMessage && verification.errorMessage.trim() !== '') ? 'Retry Processing' : 'Start Processing'}
                </button>
              )}
              <span className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium ${getStatusColor(verification.status)}`}>
                {verification.status}
              </span>
            </div>
          </div>
        </div>

        {/* Error Message */}
        {verification.errorMessage && verification.errorMessage.trim() !== '' && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-6 mb-6">
            <div className="flex items-start">
              <div className="shrink-0">
                <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                  <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
                </svg>
              </div>
              <div className="ml-3 flex-1">
                <h3 className="text-sm font-medium text-red-800 mb-2">Processing Error</h3>
                <p className="text-sm text-red-700 mb-3">{verification.errorMessage}</p>
                {verification.userActionRequired && verification.userActionRequired.trim() !== '' && (
                  <div className="bg-white rounded-md p-4 border border-red-200">
                    <h4 className="text-sm font-medium text-gray-900 mb-2">What you need to do:</h4>
                    <p className="text-sm text-gray-700">{verification.userActionRequired}</p>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Progress Stages */}
        <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-8">
          <h2 className="text-lg font-medium text-gray-900 mb-6">Processing Stages</h2>
          <div className="space-y-4">
            {stages.map((stage, index) => (
              <div key={stage.id} className="flex items-center space-x-4">
                <div className="shrink-0">
                  {getStageIcon(getStageStatus(stage))}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-medium text-gray-900">{stage.name}</p>
                    <p className="text-xs text-gray-500">
                      {getStageStatus(stage) === 'completed' ? 'Completed' :
                       getStageStatus(stage) === 'processing' ? 'Processing...' :
                       getStageStatus(stage) === 'error' ? 'Error' : 'Pending'}
                    </p>
                  </div>
                  <p className="text-sm text-gray-600 mt-1">{stage.description}</p>
                </div>
              </div>
            ))}
          </div>
        </div>

        {/* Verification Details */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
          {/* Basic Info */}
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
            <h3 className="text-lg font-medium text-gray-900 mb-4">Verification Details</h3>
            <dl className="space-y-3">
              <div>
                <dt className="text-sm font-medium text-gray-500">Document Type</dt>
                <dd className="text-sm text-gray-900">{verification.documentType}</dd>
              </div>
              <div>
                <dt className="text-sm font-medium text-gray-500">Priority</dt>
                <dd className="text-sm text-gray-900">{verification.priority}</dd>
              </div>
              <div>
                <dt className="text-sm font-medium text-gray-500">Submitted</dt>
                <dd className="text-sm text-gray-900">{new Date(verification.createdAt).toLocaleString()}</dd>
              </div>
              {verification.processingStartedAt && (
                <div>
                  <dt className="text-sm font-medium text-gray-500">Processing Started</dt>
                  <dd className="text-sm text-gray-900">{new Date(verification.processingStartedAt).toLocaleString()}</dd>
                </div>
              )}
              {verification.completedAt && (
                <div>
                  <dt className="text-sm font-medium text-gray-500">Completed</dt>
                  <dd className="text-sm text-gray-900">{new Date(verification.completedAt).toLocaleString()}</dd>
                </div>
              )}
              {verification.decisionReason && (
                <div>
                  <dt className="text-sm font-medium text-gray-500">Reason</dt>
                  <dd className="text-sm text-gray-900">{verification.decisionReason}</dd>
                </div>
              )}
            </dl>
          </div>

          {/* Results */}
          {(verification.authenticityScore || verification.faceMatchResult || verification.ocrResult) && (
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <h3 className="text-lg font-medium text-gray-900 mb-4">AI Analysis Results</h3>
              <dl className="space-y-4">
                {verification.authenticityScore && (
                  <div>
                    <dt className="text-sm font-medium text-gray-500 mb-1">Authenticity Score</dt>
                    <dd className="flex items-center justify-between">
                      <div className="flex items-center">
                        <span className="text-lg font-bold text-gray-900 mr-2">
                          {verification.authenticityScore.overallScore}/100
                        </span>
                        <span className={`text-xs px-2 py-1 rounded-full ${getStatusColor(
                          verification.authenticityScore.classification === 'Genuine' ? 'Approved' :
                          verification.authenticityScore.classification === 'Suspicious' ? 'ReviewNeeded' : 'Rejected'
                        )}`}>
                          {verification.authenticityScore.classification}
                        </span>
                      </div>
                    </dd>
                    {verification.authenticityScore.fieldCompletenessScore && (
                      <div className="mt-2 text-xs text-gray-500">
                        Field Completeness: {verification.authenticityScore.fieldCompletenessScore}% | 
                        Image Quality: {verification.authenticityScore.imageQualityScore || 'N/A'}%
                      </div>
                    )}
                  </div>
                )}
                {verification.faceMatchResult && (
                  <div>
                    <dt className="text-sm font-medium text-gray-500 mb-1">Face Matching</dt>
                    {verification.faceMatchResult.similarityScore != null && verification.faceMatchResult.matchDecision != null ? (
                      <>
                        <dd className="flex items-center justify-between">
                          <div className="flex items-center">
                            <span className="text-lg font-bold text-gray-900 mr-2">
                              {Math.round(Number(verification.faceMatchResult.similarityScore) * 100)}%
                            </span>
                            <span
                              className={`text-xs px-2 py-1 rounded-full ${
                                verification.faceMatchResult.matchDecision
                                  ? 'text-green-600 bg-green-100'
                                  : 'text-red-600 bg-red-100'
                              }`}
                            >
                              {verification.faceMatchResult.matchDecision ? 'Match' : 'No Match'}
                            </span>
                          </div>
                        </dd>
                        <div className="mt-2 text-xs text-gray-500">
                          ID Face: {verification.faceMatchResult.idFaceDetected ? 'Detected ✓' : 'Not Detected ✗'} |{' '}
                          Selfie: {verification.faceMatchResult.selfieFaceDetected ? 'Detected ✓' : 'Not Detected ✗'}
                        </div>
                      </>
                    ) : (
                      <div className="text-sm text-gray-600 mt-1">
                        Face could not be reliably recognized
                        {' '}
                        {(!verification.faceMatchResult.idFaceDetected || !verification.faceMatchResult.selfieFaceDetected) && (
                          <>
                            – ID Face: {verification.faceMatchResult.idFaceDetected ? 'Detected ✓' : 'Not Detected ✗'}; Selfie:{' '}
                            {verification.faceMatchResult.selfieFaceDetected ? 'Detected ✓' : 'Not Detected ✗'}
                          </>
                        )}
                      </div>
                    )}
                  </div>
                )}
                {verification.ocrResult && (
                  <div>
                    <dt className="text-sm font-medium text-gray-500 mb-1">OCR Confidence</dt>
                    <dd className="flex items-center justify-between">
                      <span className="text-sm font-medium text-gray-900">
                        {Math.round(Number(verification.ocrResult.confidenceScore || 0) * 100)}%
                      </span>
                      {verification.ocrResult.languageDetected && (
                        <span className="text-xs text-gray-500">
                          Language: {verification.ocrResult.languageDetected}
                        </span>
                      )}
                    </dd>
                  </div>
                )}
              </dl>
            </div>
          )}
        </div>

        {/* Action Buttons */}
        <div className="flex justify-between items-center">
          <button
            onClick={() => setShowDeleteConfirm(true)}
            disabled={isDeleting}
            className="px-4 py-2 bg-red-600 text-white rounded-md hover:bg-red-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {isDeleting ? 'Deleting...' : 'Delete Verification'}
          </button>
          
          <div className="flex flex-wrap gap-3">
            <button
              onClick={() => router.push(`/review/${verification.id}`)}
              className="px-4 py-2 bg-indigo-600 text-white rounded-md hover:bg-indigo-700"
            >
              Review
            </button>
            {verification.status === 'Approved' && (
              <button
                onClick={() => router.push(`/verify/${verificationId}/status`)}
                className="px-4 py-2 bg-green-600 text-white rounded-md hover:bg-green-700"
              >
                View Results
              </button>
            )}
            <button
              onClick={() => router.push('/dashboard')}
              className="px-4 py-2 bg-gray-600 text-white rounded-md hover:bg-gray-700"
            >
              Return to Dashboard
            </button>
          </div>
        </div>

        {/* Delete Confirmation Modal */}
        {showDeleteConfirm && (
          <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-white rounded-lg p-6 max-w-md w-full mx-4">
              <h3 className="text-lg font-medium text-gray-900 mb-4">Delete Verification</h3>
              <p className="text-sm text-gray-600 mb-6">
                Are you sure you want to delete this verification? This action cannot be undone. All associated documents and data will be permanently removed.
              </p>
              <div className="flex justify-end space-x-3">
                <button
                  onClick={() => setShowDeleteConfirm(false)}
                  disabled={isDeleting}
                  className="px-4 py-2 border border-gray-300 rounded-md text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
                >
                  Cancel
                </button>
                <button
                  onClick={handleDelete}
                  disabled={isDeleting}
                  className="px-4 py-2 bg-red-600 text-white rounded-md text-sm font-medium hover:bg-red-700 disabled:opacity-50"
                >
                  {isDeleting ? 'Deleting...' : 'Delete'}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </DashboardLayout>
  );
}