'use client';

import { useState, useEffect } from 'react';
import { useParams, useRouter } from 'next/navigation';
import DashboardLayout from '@/components/layout/dashboard-layout';
import { VerificationDto } from '@/types/shared';
import { verificationService } from '@/lib/api/verification-service';
import { Button } from '@/components/ui/button';
import { authService } from '@/lib/auth/auth-service';

export default function ReviewDetailPage() {
  const params = useParams();
  const router = useRouter();
  const verificationId = params.id as string;

  const [verification, setVerification] = useState<VerificationDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [decision, setDecision] = useState<'approve' | 'reject' | null>(null);
  const [notes, setNotes] = useState('');
  const [previewUrls, setPreviewUrls] = useState<Record<string, string>>({});

  useEffect(() => {
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

    if (verificationId) {
      loadVerification();
    }
  }, [verificationId]);

  // Load authenticated preview URLs for image documents
  useEffect(() => {
    const loadPreviews = async () => {
      if (!verification || !verification.documents || verification.documents.length === 0) return;

      const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';
      const authHeaders = authService.getAuthHeader();

      const newUrls: Record<string, string> = {};

      await Promise.all(
        verification.documents.map(async (doc) => {
          if (!doc.mimeType?.startsWith('image/')) return;

          const endpoint = `${API_BASE_URL}/verification/${verification.id}/document/${doc.id}`;
          try {
            const response = await fetch(endpoint, { headers: authHeaders });
            if (!response.ok) {
              return;
            }
            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            newUrls[doc.id] = url;
          } catch {
            // Ignore preview errors; UI will show fallback text
          }
        })
      );

      if (Object.keys(newUrls).length > 0) {
        setPreviewUrls((prev) => ({ ...prev, ...newUrls }));
      }
    };

    loadPreviews();

    // Cleanup created blob URLs on unmount / verification change
    return () => {
      Object.values(previewUrls).forEach((url) => URL.revokeObjectURL(url));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [verification]);

  const handleApprove = async () => {
    if (!verification) return;

    setIsProcessing(true);
    try {
      await verificationService.updateVerificationStatus(verificationId, {
        status: 'Approved',
        reason: notes || 'Manually approved by verification officer',
      });
      router.push('/review');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to approve verification');
    } finally {
      setIsProcessing(false);
    }
  };

  const handleReject = async () => {
    if (!verification) return;

    setIsProcessing(true);
    try {
      await verificationService.updateVerificationStatus(verificationId, {
        status: 'Rejected',
        reason: notes || 'Manually rejected by verification officer',
      });
      router.push('/review');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to reject verification');
    } finally {
      setIsProcessing(false);
    }
  };

  const getStatusColor = (status: string) => {
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
        <div className="max-w-6xl mx-auto p-6">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-200 rounded w-1/3 mb-8"></div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="bg-white p-6 rounded-lg shadow">
                <div className="h-4 bg-gray-200 rounded w-1/2 mb-4"></div>
                <div className="space-y-3">
                  {[1, 2, 3, 4].map((i) => (
                    <div key={i} className="h-3 bg-gray-200 rounded"></div>
                  ))}
                </div>
              </div>
              <div className="bg-white p-6 rounded-lg shadow">
                <div className="h-4 bg-gray-200 rounded w-1/2 mb-4"></div>
                <div className="space-y-3">
                  {[1, 2, 3].map((i) => (
                    <div key={i} className="h-3 bg-gray-200 rounded"></div>
                  ))}
                </div>
              </div>
            </div>
          </div>
        </div>
      </DashboardLayout>
    );
  }

  if (error || !verification) {
    return (
      <DashboardLayout>
        <div className="max-w-6xl mx-auto p-6">
          <div className="text-center">
            <div className="text-red-600 text-4xl mb-4">❌</div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">Error Loading Verification</h2>
            <p className="text-gray-600 mb-6">{error || 'Verification not found'}</p>
            <Button onClick={() => router.push('/review')}>Return to Review Queue</Button>
          </div>
        </div>
      </DashboardLayout>
    );
  }

  return (
    <DashboardLayout>
      <div className="max-w-6xl mx-auto p-6">
        {/* Header */}
        <div className="mb-8">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-3xl font-bold text-gray-900">Review Verification</h1>
              <p className="mt-2 text-gray-600">
                Reference: <span className="font-medium">{verification.referenceNumber}</span>
              </p>
            </div>
            <div className="text-right">
              <span className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium ${getStatusColor(verification.status)}`}>
                {verification.status}
              </span>
            </div>
          </div>
        </div>

        {/* Error Display */}
        {error && (
          <div className="mb-6 bg-red-50 border border-red-200 rounded-md p-4">
            <div className="text-sm text-red-600">{error}</div>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Main Content */}
          <div className="lg:col-span-2 space-y-6">
            {/* Verification Details */}
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <h2 className="text-lg font-medium text-gray-900 mb-4">Verification Details</h2>
              <dl className="grid grid-cols-2 gap-4">
                <div>
                  <dt className="text-sm font-medium text-gray-500">Document Type</dt>
                  <dd className="text-sm text-gray-900 mt-1">{verification.documentType}</dd>
                </div>
                <div>
                  <dt className="text-sm font-medium text-gray-500">Priority</dt>
                  <dd className="text-sm text-gray-900 mt-1">{verification.priority}</dd>
                </div>
                <div>
                  <dt className="text-sm font-medium text-gray-500">Submitted</dt>
                  <dd className="text-sm text-gray-900 mt-1">
                    {new Date(verification.createdAt).toLocaleString()}
                  </dd>
                </div>
                {verification.processingStartedAt && (
                  <div>
                    <dt className="text-sm font-medium text-gray-500">Processing Started</dt>
                    <dd className="text-sm text-gray-900 mt-1">
                      {new Date(verification.processingStartedAt).toLocaleString()}
                    </dd>
                  </div>
                )}
                {verification.completedAt && (
                  <div>
                    <dt className="text-sm font-medium text-gray-500">Completed</dt>
                    <dd className="text-sm text-gray-900 mt-1">
                      {new Date(verification.completedAt).toLocaleString()}
                    </dd>
                  </div>
                )}
                {verification.errorMessage && (
                  <div className="col-span-2">
                    <dt className="text-sm font-medium text-red-500">Error</dt>
                    <dd className="text-sm text-red-700 mt-1">{verification.errorMessage}</dd>
                  </div>
                )}
                {verification.userActionRequired && (
                  <div className="col-span-2">
                    <dt className="text-sm font-medium text-yellow-500">Action Required</dt>
                    <dd className="text-sm text-yellow-700 mt-1">{verification.userActionRequired}</dd>
                  </div>
                )}
              </dl>
            </div>

            {/* Documents */}
            {verification.documents && verification.documents.length > 0 && (
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <h2 className="text-lg font-medium text-gray-900 mb-4">Documents</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {verification.documents.map((doc) => {
                    const isImage = doc.mimeType?.startsWith('image/');
                    const previewUrl = isImage ? previewUrls[doc.id] : undefined;

                    return (
                      <div key={doc.id} className="border border-gray-200 rounded-lg p-4">
                        <div className="flex items-center justify-between mb-3">
                          <span className="text-sm font-medium text-gray-900 capitalize">
                            {doc.documentType.replace(/([A-Z])/g, ' $1').trim()}
                          </span>
                          <span className="text-xs text-gray-500">
                            {(doc.fileSizeBytes / 1024).toFixed(2)} KB
                          </span>
                        </div>

                        {isImage ? (
                          <div className="mb-3">
                            {previewUrl ? (
                              <img
                                src={previewUrl}
                                alt={doc.documentType}
                                className="w-full h-48 object-contain bg-gray-50 rounded-lg border border-gray-200"
                                onError={(e) => {
                                  const target = e.target as HTMLImageElement;
                                  target.style.display = 'none';
                                  const parent = target.parentElement;
                                  if (parent) {
                                    parent.innerHTML =
                                      '<div class="text-center py-8 text-gray-400">Image not available</div>';
                                  }
                                }}
                              />
                            ) : (
                              <div className="flex items-center justify-center h-48 bg-gray-50 rounded-lg border border-dashed border-gray-200 text-xs text-gray-400">
                                Loading preview...
                              </div>
                            )}
                          </div>
                        ) : (
                          <div className="mb-3 bg-gray-50 rounded-lg border border-gray-200 p-8 text-center">
                            <svg className="mx-auto h-12 w-12 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                            </svg>
                            <p className="mt-2 text-xs text-gray-500">{doc.fileName}</p>
                          </div>
                        )}

                        <div className="flex gap-2">
                          <button
                            type="button"
                            onClick={() => {
                              if (previewUrl) {
                                window.open(previewUrl, '_blank', 'noopener,noreferrer');
                              } else if (isImage) {
                                // Fallback to download if preview not ready
                                void verificationService.downloadDocument(
                                  verification.id,
                                  doc.id,
                                  doc.originalFileName || doc.fileName
                                );
                              }
                            }}
                            className="flex-1 text-center text-sm px-3 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
                          >
                            View Full
                          </button>
                          <button
                            type="button"
                            onClick={() =>
                              verificationService.downloadDocument(
                                verification.id,
                                doc.id,
                                doc.originalFileName || doc.fileName
                              )
                            }
                            className="flex-1 text-center text-sm px-3 py-2 bg-gray-100 text-gray-700 rounded-md hover:bg-gray-200 transition-colors"
                          >
                            Download
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {/* OCR Extracted Fields */}
            {verification.ocrResult && (
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <div className="flex items-center justify-between mb-4">
                  <h2 className="text-lg font-medium text-gray-900">Extracted Data</h2>
                  {verification.ocrResult.rawText && (
                    <button
                      onClick={() => {
                        const modal = document.getElementById('ocr-raw-text-modal');
                        if (modal) {
                          modal.classList.remove('hidden');
                        }
                      }}
                      className="text-sm text-blue-600 hover:text-blue-800 font-medium"
                    >
                      View Raw Text →
                    </button>
                  )}
                </div>
                
                {verification.ocrResult.extractedFields && Object.keys(verification.ocrResult.extractedFields).length > 0 ? (
                  <dl className="grid grid-cols-2 gap-4">
                    {Object.entries(verification.ocrResult.extractedFields).map(([key, field]) => (
                      <div key={key} className="border-l-2 border-blue-200 pl-3">
                        <dt className="text-sm font-medium text-gray-500 capitalize">
                          {key.replace(/([A-Z])/g, ' $1').trim()}
                        </dt>
                        <dd className="text-sm text-gray-900 mt-1">
                          <span className="font-medium">{field.value || 'N/A'}</span>
                          {field.confidence && (
                            <span className={`text-xs ml-2 px-2 py-0.5 rounded ${
                              field.confidence >= 0.9 ? 'bg-green-100 text-green-700' :
                              field.confidence >= 0.7 ? 'bg-yellow-100 text-yellow-700' :
                              'bg-red-100 text-red-700'
                            }`}>
                              {Math.round(field.confidence * 100)}%
                            </span>
                          )}
                        </dd>
                      </div>
                    ))}
                  </dl>
                ) : (
                  <p className="text-sm text-gray-500">No fields extracted</p>
                )}
              </div>
            )}

            {/* OCR Raw Text Modal */}
            {verification.ocrResult?.rawText && (
              <div
                id="ocr-raw-text-modal"
                className="hidden fixed inset-0 z-50 overflow-y-auto"
                onClick={(e) => {
                  if (e.target === e.currentTarget) {
                    e.currentTarget.classList.add('hidden');
                  }
                }}
              >
                <div className="flex items-center justify-center min-h-screen px-4">
                  <div className="bg-white rounded-lg shadow-xl max-w-4xl w-full max-h-[90vh] flex flex-col">
                    <div className="flex items-center justify-between p-6 border-b border-gray-200">
                      <h3 className="text-lg font-bold text-gray-900">OCR Raw Text</h3>
                      <button
                        onClick={() => {
                          const modal = document.getElementById('ocr-raw-text-modal');
                          if (modal) {
                            modal.classList.add('hidden');
                          }
                        }}
                        className="text-gray-400 hover:text-gray-600"
                      >
                        <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                        </svg>
                      </button>
                    </div>
                    <div className="p-6 overflow-y-auto flex-1">
                      <div className="bg-gray-50 rounded-lg p-4">
                        <pre className="text-xs text-gray-700 whitespace-pre-wrap font-mono">
                          {verification.ocrResult.rawText}
                        </pre>
                      </div>
                    </div>
                    <div className="p-6 border-t border-gray-200 flex justify-end">
                      <Button
                        onClick={() => {
                          const modal = document.getElementById('ocr-raw-text-modal');
                          if (modal) {
                            modal.classList.add('hidden');
                          }
                        }}
                        variant="outline"
                      >
                        Close
                      </Button>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* AI Analysis Results */}
            {(verification.authenticityScore || verification.faceMatchResult || verification.ocrResult) && (
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <h2 className="text-lg font-medium text-gray-900 mb-4">AI Analysis Results</h2>

                {verification.authenticityScore && (
                  <div className="mb-6">
                    <h3 className="text-sm font-medium text-gray-700 mb-3">Authenticity Score</h3>
                    <div className="space-y-3">
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-gray-600">Overall Score</span>
                        <span className="text-lg font-bold text-gray-900">
                          {verification.authenticityScore.overallScore}/100
                        </span>
                      </div>
                      <div className="w-full bg-gray-200 rounded-full h-3">
                        <div
                          className={`h-3 rounded-full transition-all ${
                            verification.authenticityScore.overallScore >= 80
                              ? 'bg-green-600'
                              : verification.authenticityScore.overallScore >= 50
                              ? 'bg-yellow-600'
                              : 'bg-red-600'
                          }`}
                          style={{ width: `${verification.authenticityScore.overallScore}%` }}
                        ></div>
                      </div>
                      <div className="flex items-center justify-between text-xs text-gray-500">
                        <span className="font-medium">Classification: {verification.authenticityScore.classification}</span>
                      </div>
                      
                      {/* Detailed Score Breakdown */}
                      <div className="mt-4 pt-4 border-t border-gray-200">
                        <h4 className="text-xs font-medium text-gray-700 mb-2">Score Breakdown</h4>
                        <div className="space-y-2">
                          {verification.authenticityScore.fieldCompletenessScore != null && (
                            <div>
                              <div className="flex justify-between text-xs mb-1">
                                <span className="text-gray-600">Field Completeness</span>
                                <span className="text-gray-900 font-medium">{verification.authenticityScore.fieldCompletenessScore}%</span>
                              </div>
                              <div className="w-full bg-gray-100 rounded-full h-1.5">
                                <div className="bg-blue-600 h-1.5 rounded-full" style={{ width: `${verification.authenticityScore.fieldCompletenessScore}%` }}></div>
                              </div>
                            </div>
                          )}
                          {verification.authenticityScore.formatConsistencyScore != null && (
                            <div>
                              <div className="flex justify-between text-xs mb-1">
                                <span className="text-gray-600">Format Consistency</span>
                                <span className="text-gray-900 font-medium">{verification.authenticityScore.formatConsistencyScore}%</span>
                              </div>
                              <div className="w-full bg-gray-100 rounded-full h-1.5">
                                <div className="bg-purple-600 h-1.5 rounded-full" style={{ width: `${verification.authenticityScore.formatConsistencyScore}%` }}></div>
                              </div>
                            </div>
                          )}
                          {verification.authenticityScore.imageQualityScore != null && (
                            <div>
                              <div className="flex justify-between text-xs mb-1">
                                <span className="text-gray-600">Image Quality</span>
                                <span className="text-gray-900 font-medium">{verification.authenticityScore.imageQualityScore}%</span>
                              </div>
                              <div className="w-full bg-gray-100 rounded-full h-1.5">
                                <div className="bg-green-600 h-1.5 rounded-full" style={{ width: `${verification.authenticityScore.imageQualityScore}%` }}></div>
                              </div>
                            </div>
                          )}
                          {verification.authenticityScore.securityFeaturesScore != null && (
                            <div>
                              <div className="flex justify-between text-xs mb-1">
                                <span className="text-gray-600">Security Features</span>
                                <span className="text-gray-900 font-medium">{verification.authenticityScore.securityFeaturesScore}%</span>
                              </div>
                              <div className="w-full bg-gray-100 rounded-full h-1.5">
                                <div className="bg-orange-600 h-1.5 rounded-full" style={{ width: `${verification.authenticityScore.securityFeaturesScore}%` }}></div>
                              </div>
                            </div>
                          )}
                          {verification.authenticityScore.metadataConsistencyScore != null && (
                            <div>
                              <div className="flex justify-between text-xs mb-1">
                                <span className="text-gray-600">Metadata Consistency</span>
                                <span className="text-gray-900 font-medium">{verification.authenticityScore.metadataConsistencyScore}%</span>
                              </div>
                              <div className="w-full bg-gray-100 rounded-full h-1.5">
                                <div className="bg-indigo-600 h-1.5 rounded-full" style={{ width: `${verification.authenticityScore.metadataConsistencyScore}%` }}></div>
                              </div>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  </div>
                )}

                {verification.faceMatchResult && (
                  <div className="mb-6">
                    <h3 className="text-sm font-medium text-gray-700 mb-3">Face Matching</h3>
                    <div className="space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-gray-600">Similarity Score</span>
                        <span className="text-lg font-bold text-gray-900">
                          {Math.round(Number(verification.faceMatchResult.similarityScore || 0) * 100)}%
                        </span>
                      </div>
                      <div className="w-full bg-gray-200 rounded-full h-2">
                        <div
                          className={`h-2 rounded-full ${verification.faceMatchResult.matchDecision ? 'bg-green-600' : 'bg-red-600'
                            }`}
                          style={{
                            width: `${Number(verification.faceMatchResult.similarityScore || 0) * 100}%`,
                          }}
                        ></div>
                      </div>
                      <div className="flex items-center justify-between text-xs text-gray-500 mt-1">
                        <span>
                          Match: {verification.faceMatchResult.matchDecision ? 'Yes' : 'No'}
                        </span>
                        <span>
                          ID Face: {verification.faceMatchResult.idFaceDetected ? 'Detected' : 'Not Detected'} |
                          Selfie: {verification.faceMatchResult.selfieFaceDetected ? 'Detected' : 'Not Detected'}
                        </span>
                      </div>
                    </div>
                  </div>
                )}

                {verification.ocrResult && (
                  <div>
                    <h3 className="text-sm font-medium text-gray-700 mb-3">OCR Results</h3>
                    <div className="space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-gray-600">Confidence Score</span>
                        <span className="text-sm font-medium text-gray-900">
                          {verification.ocrResult.confidenceScore != null
                            ? Math.round(Number(verification.ocrResult.confidenceScore) * 100)
                            : 0}%
                        </span>
                      </div>
                      {verification.ocrResult.languageDetected && (
                        <div className="text-xs text-gray-500">
                          Language: {verification.ocrResult.languageDetected}
                        </div>
                      )}
                      {verification.ocrResult.processingTimeMs && (
                        <div className="text-xs text-gray-500">
                          Processing Time: {verification.ocrResult.processingTimeMs}ms
                        </div>
                      )}
                    </div>
                  </div>
                )}
              </div>
            )}

            {/* Decision Reason */}
            {verification.decisionReason && (
              <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
                <h2 className="text-lg font-medium text-gray-900 mb-2">Decision Reason</h2>
                <p className="text-sm text-gray-600">{verification.decisionReason}</p>
              </div>
            )}
          </div>

          {/* Sidebar - Review Actions */}
          <div className="space-y-6">
            <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6">
              <h2 className="text-lg font-medium text-gray-900 mb-4">Review Actions</h2>

              <div className="space-y-4">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-2">
                    Review Notes
                  </label>
                  <textarea
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                    rows={4}
                    className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm text-gray-900 bg-white focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                    placeholder="Add notes about your review decision..."
                  />
                </div>

                <div className="flex flex-col space-y-2">
                  <Button
                    onClick={handleApprove}
                    disabled={isProcessing}
                    className="w-full bg-green-600 hover:bg-green-700"
                  >
                    {isProcessing ? 'Processing...' : 'Approve Verification'}
                  </Button>
                  <Button
                    onClick={handleReject}
                    disabled={isProcessing}
                    variant="outline"
                    className="w-full border-red-600 text-red-600 hover:bg-red-50"
                  >
                    {isProcessing ? 'Processing...' : 'Reject Verification'}
                  </Button>
                  <Button
                    onClick={() => router.push('/review')}
                    variant="outline"
                    className="w-full"
                  >
                    Cancel
                  </Button>
                </div>
              </div>
            </div>

            {/* Quick Info */}
            <div className="bg-blue-50 rounded-lg border border-blue-200 p-4">
              <h3 className="text-sm font-medium text-blue-900 mb-2">Review Guidelines</h3>
              <ul className="text-xs text-blue-800 space-y-1">
                <li>• Check authenticity scores carefully</li>
                <li>• Verify face match results</li>
                <li>• Review OCR extracted data</li>
                <li>• Add detailed notes for your decision</li>
              </ul>
            </div>
          </div>
        </div>
      </div>
    </DashboardLayout>
  );
}

