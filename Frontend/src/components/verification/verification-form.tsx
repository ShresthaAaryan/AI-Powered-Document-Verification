'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import DashboardLayout from '@/components/layout/dashboard-layout';
import DocumentUpload from '@/components/verification/document-upload';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { CreateVerificationRequest, DocumentType, Priority } from '@/types/shared';
import { verificationService } from '@/lib/api/verification-service';

export default function VerificationForm() {
  const router = useRouter();
  const [formData, setFormData] = useState<CreateVerificationRequest>({
    documentType: 'Passport',
    idDocument: null as any,
    selfieImage: null as any,
    applicantName: '',
    dateOfBirth: '',
    referenceNumber: '',
    priority: 'Normal',
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const documentTypes: { value: DocumentType; label: string }[] = [
    { value: 'Passport', label: 'Passport' },
    { value: 'DriversLicense', label: "Driver's License" },
    { value: 'NationalID', label: 'National ID' },
    { value: 'CitizenshipCard', label: 'Citizenship Card' },
  ];

  const priorities: { value: Priority; label: string }[] = [
    { value: 'Low', label: 'Low' },
    { value: 'Normal', label: 'Normal' },
    { value: 'High', label: 'High' },
    { value: 'Urgent', label: 'Urgent' },
  ];

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    const updates: Partial<CreateVerificationRequest> = { [name]: value };
    if (name === 'documentType' && value !== 'CitizenshipCard') {
      updates.idDocumentBack = undefined;
    }
    setFormData(prev => ({ ...prev, ...updates }));
    if (errors[name]) setErrors(prev => ({ ...prev, [name]: '' }));
  };

  const handleFilesSelected = (files: { idDocument?: File; selfieImage?: File; idDocumentBack?: File }) => {
    setFormData(prev => ({
      ...prev,
      ...files,
    }));

    if (files.idDocument && errors.idDocument) setErrors(prev => ({ ...prev, idDocument: '' }));
    if (files.selfieImage && errors.selfieImage) setErrors(prev => ({ ...prev, selfieImage: '' }));
    if (files.idDocumentBack && errors.idDocumentBack) setErrors(prev => ({ ...prev, idDocumentBack: '' }));
  };

  const validateForm = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!formData.idDocument) {
      newErrors.idDocument = 'ID document is required';
    }

    if (!formData.selfieImage) {
      newErrors.selfieImage = 'Selfie image is required';
    }

    if (formData.applicantName && formData.applicantName.trim().length < 2) {
      newErrors.applicantName = 'Applicant name must be at least 2 characters';
    }

    if (formData.dateOfBirth) {
      const dob = new Date(formData.dateOfBirth);
      const now = new Date();
      if (dob >= now) {
        newErrors.dateOfBirth = 'Date of birth must be in the past';
      }
    }

    if (formData.referenceNumber && formData.referenceNumber.trim().length < 3) {
      newErrors.referenceNumber = 'Reference number must be at least 3 characters';
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!validateForm()) {
      return;
    }

    setIsSubmitting(true);

    try {
      const verification = await verificationService.createVerification(formData);
      router.push(`/verify/${verification.id}/status`);
    } catch (error) {
      console.error('Verification creation failed:', error);
      setErrors({ submit: error instanceof Error ? error.message : 'Failed to create verification' });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <DashboardLayout>
      <div className="max-w-4xl mx-auto p-6">
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900">New Verification</h1>
          <p className="mt-2 text-gray-600">
            Upload identity documents for AI-powered verification
          </p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-8">
          {/* Document Type Selection */}
          <div>
            <label htmlFor="documentType" className="block text-sm font-medium text-gray-700 mb-2">
              Document Type
            </label>
            <select
              id="documentType"
              name="documentType"
              value={formData.documentType}
              onChange={handleInputChange}
              className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm text-gray-900 bg-white focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
            >
              {documentTypes.map(type => (
                <option key={type.value} value={type.value}>
                  {type.label}
                </option>
              ))}
            </select>
          </div>

          {/* File Upload */}
          <DocumentUpload
            documentType={formData.documentType}
            onFilesSelected={handleFilesSelected}
            errors={errors}
          />

          {/* Applicant Information */}
          <div className="bg-gray-50 rounded-lg p-6">
            <h3 className="text-lg font-medium text-gray-900 mb-4">Applicant Information (Optional)</h3>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label htmlFor="applicantName" className="block text-sm font-medium text-gray-700 mb-1">
                  Full Name
                </label>
                <Input
                  id="applicantName"
                  name="applicantName"
                  type="text"
                  placeholder="Enter full name"
                  value={formData.applicantName}
                  onChange={handleInputChange}
                  error={errors.applicantName}
                />
              </div>

              <div>
                <label htmlFor="dateOfBirth" className="block text-sm font-medium text-gray-700 mb-1">
                  Date of Birth
                </label>
                <Input
                  id="dateOfBirth"
                  name="dateOfBirth"
                  type="date"
                  value={formData.dateOfBirth}
                  onChange={handleInputChange}
                  error={errors.dateOfBirth}
                />
              </div>

              <div>
                <label htmlFor="referenceNumber" className="block text-sm font-medium text-gray-700 mb-1">
                  Reference Number
                </label>
                <Input
                  id="referenceNumber"
                  name="referenceNumber"
                  type="text"
                  placeholder="Enter reference number"
                  value={formData.referenceNumber}
                  onChange={handleInputChange}
                  error={errors.referenceNumber}
                />
              </div>

              <div>
                <label htmlFor="priority" className="block text-sm font-medium text-gray-700 mb-1">
                  Priority
                </label>
                <select
                  id="priority"
                  name="priority"
                  value={formData.priority}
                  onChange={handleInputChange}
                  className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                >
                  {priorities.map(priority => (
                    <option key={priority.value} value={priority.value}>
                      {priority.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>

          {/* Error Display */}
          {errors.submit && (
            <div className="bg-red-50 border border-red-200 rounded-md p-4">
              <div className="text-sm text-red-600">{errors.submit}</div>
            </div>
          )}

          {/* Submit Buttons */}
          <div className="flex justify-end space-x-4">
            <Button
              type="button"
              variant="outline"
              onClick={() => router.back()}
              disabled={isSubmitting}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={isSubmitting || !formData.idDocument || !formData.selfieImage}
              isLoading={isSubmitting}
            >
              {isSubmitting ? 'Creating Verification...' : 'Create Verification'}
            </Button>
          </div>
        </form>
      </div>
    </DashboardLayout>
  );
}