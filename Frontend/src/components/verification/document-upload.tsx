'use client';

import { useState, useCallback, useEffect } from 'react';
import { CreateVerificationRequest, DocumentType } from '@/types/shared';

type FileInputType = 'idDocument' | 'selfieImage' | 'idDocumentBack';

interface DocumentUploadProps {
  onFilesSelected: (files: { idDocument?: File; selfieImage?: File; idDocumentBack?: File }) => void;
  documentType: DocumentType;
  errors?: Record<string, string>;
}

export default function DocumentUpload({ onFilesSelected, documentType, errors }: DocumentUploadProps) {
  const [dragActive, setDragActive] = useState(false);
  const [idDocument, setIdDocument] = useState<File | null>(null);
  const [idDocumentBack, setIdDocumentBack] = useState<File | null>(null);
  const [selfieImage, setSelfieImage] = useState<File | null>(null);

  const handleDrag = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === 'dragenter' || e.type === 'dragover') {
      setDragActive(true);
    } else if (e.type === 'dragleave') {
      setDragActive(false);
    }
  }, []);

  const handleDrop = useCallback((e: React.DragEvent, fileType: FileInputType) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);

    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      handleFileSelect(e.dataTransfer.files[0], fileType);
    }
  }, []);

  const handleFileSelect = (file: File, fileType: FileInputType) => {
    const isValid = validateFile(file, fileType);
    if (!isValid) return;

    if (fileType === 'idDocument') {
      setIdDocument(file);
      onFilesSelected({ idDocument: file, selfieImage: selfieImage ?? undefined, idDocumentBack: idDocumentBack ?? undefined });
    } else if (fileType === 'idDocumentBack') {
      setIdDocumentBack(file);
      onFilesSelected({ idDocument: idDocument ?? undefined, selfieImage: selfieImage ?? undefined, idDocumentBack: file });
    } else {
      setSelfieImage(file);
      onFilesSelected({ idDocument: idDocument ?? undefined, selfieImage: file, idDocumentBack: idDocumentBack ?? undefined });
    }
  };

  const validateFile = (file: File, fileType: FileInputType): boolean => {
    const maxSize = fileType === 'selfieImage' ? 5 * 1024 * 1024 : 10 * 1024 * 1024;
    if (file.size > maxSize) {
      alert(`File size must be less than ${maxSize / (1024 * 1024)}MB`);
      return false;
    }
    const allowedTypes = fileType === 'selfieImage'
      ? ['image/jpeg', 'image/jpg', 'image/png', 'image/bmp']
      : ['image/jpeg', 'image/jpg', 'image/png', 'image/bmp', 'image/tiff', 'application/pdf'];
    if (!allowedTypes.includes(file.type)) {
      alert(`Invalid file type. Allowed types: ${allowedTypes.join(', ')}`);
      return false;
    }
    return true;
  };

  const removeFile = (fileType: FileInputType) => {
    if (fileType === 'idDocument') {
      setIdDocument(null);
      onFilesSelected({ selfieImage: selfieImage ?? undefined, idDocumentBack: idDocumentBack ?? undefined });
    } else if (fileType === 'idDocumentBack') {
      setIdDocumentBack(null);
      onFilesSelected({ idDocument: idDocument ?? undefined, selfieImage: selfieImage ?? undefined });
    } else {
      setSelfieImage(null);
      onFilesSelected({ idDocument: idDocument ?? undefined, idDocumentBack: idDocumentBack ?? undefined });
    }
  };

  // Clear back when document type is no longer Citizenship
  useEffect(() => {
    if (documentType !== 'CitizenshipCard' && idDocumentBack) {
      setIdDocumentBack(null);
      onFilesSelected({ idDocument: idDocument ?? undefined, selfieImage: selfieImage ?? undefined, idDocumentBack: undefined });
    }
  }, [documentType, idDocumentBack]);

  return (
    <div className="space-y-6">
      {/* ID Document Upload */}
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-2">
          {documentType === 'Passport' ? 'Passport' : documentType === 'CitizenshipCard' ? 'Citizenship Card (Front)' : 'ID Document'}
        </label>
        {documentType === 'CitizenshipCard' && (
          <p className="text-xs text-gray-500 mb-2">
            The <strong>front</strong> has your photo and data in Nepali. It is used for <strong>face matching</strong>. Upload it here (required).
          </p>
        )}
        <div
          className={`relative border-2 border-dashed rounded-lg p-6 text-center transition-colors ${
            dragActive ? 'border-blue-400 bg-blue-50' : 'border-gray-300'
          } ${errors?.idDocument ? 'border-red-400 bg-red-50' : ''}`}
          onDragEnter={handleDrag}
          onDragLeave={handleDrag}
          onDragOver={handleDrag}
          onDrop={(e) => handleDrop(e, 'idDocument')}
        >
          {idDocument ? (
            <div className="space-y-4">
              <div className="flex items-center justify-center">
                <div className="bg-green-100 rounded-full p-3">
                  <svg className="h-8 w-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
              </div>
              <div>
                <p className="text-sm font-medium text-gray-900">{idDocument.name}</p>
                <p className="text-xs text-gray-500">{(idDocument.size / (1024 * 1024)).toFixed(2)} MB</p>
              </div>
              <button
                type="button"
                onClick={() => removeFile('idDocument')}
                className="text-red-600 hover:text-red-800 text-sm font-medium"
              >
                Remove File
              </button>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="flex items-center justify-center">
                <div className="bg-gray-100 rounded-full p-3">
                  <svg className="h-8 w-8 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                  </svg>
                </div>
              </div>
              <div>
                <p className="text-sm text-gray-600">
                  Drag and drop your {documentType === 'Passport' ? 'passport' : documentType === 'CitizenshipCard' ? 'citizenship card (front side)' : 'ID document'} here, or{' '}
                  <label className="text-blue-600 hover:text-blue-800 cursor-pointer font-medium">
                    browse
                    <input
                      type="file"
                      className="sr-only"
                      accept="image/jpeg,image/jpg,image/png,image/bmp,image/tiff,application/pdf"
                      onChange={(e) => e.target.files?.[0] && handleFileSelect(e.target.files[0], 'idDocument')}
                    />
                  </label>
                </p>
                <p className="text-xs text-gray-500 mt-1">
                  Supported formats: JPG, PNG, BMP, TIFF, PDF (Max 10MB)
                </p>
              </div>
            </div>
          )}
        </div>
        {errors?.idDocument && (
          <p className="mt-1 text-sm text-red-600">{errors.idDocument}</p>
        )}
      </div>

      {/* Citizenship Card (Back) — optional, Citizenship only */}
      {documentType === 'CitizenshipCard' && (
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-2">
            Citizenship Card (Back) <span className="text-gray-400 font-normal">— optional</span>
          </label>
          <p className="text-xs text-gray-500 mb-2">
            The back has the same data in <strong>English</strong>. It is used for <strong>data extraction</strong> (OCR). Uploading it improves accuracy.
          </p>
          <div
            className={`relative border-2 border-dashed rounded-lg p-6 text-center transition-colors ${
              dragActive ? 'border-blue-400 bg-blue-50' : 'border-gray-300'
            } ${errors?.idDocumentBack ? 'border-red-400 bg-red-50' : ''}`}
            onDragEnter={handleDrag}
            onDragLeave={handleDrag}
            onDragOver={handleDrag}
            onDrop={(e) => handleDrop(e, 'idDocumentBack')}
          >
            {idDocumentBack ? (
              <div className="space-y-4">
                <div className="flex items-center justify-center">
                  <div className="bg-green-100 rounded-full p-3">
                    <svg className="h-8 w-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                  </div>
                </div>
                <div>
                  <p className="text-sm font-medium text-gray-900">{idDocumentBack.name}</p>
                  <p className="text-xs text-gray-500">{(idDocumentBack.size / (1024 * 1024)).toFixed(2)} MB</p>
                </div>
                <button
                  type="button"
                  onClick={() => removeFile('idDocumentBack')}
                  className="text-red-600 hover:text-red-800 text-sm font-medium"
                >
                  Remove File
                </button>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center justify-center">
                  <div className="bg-gray-100 rounded-full p-3">
                    <svg className="h-8 w-8 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                    </svg>
                  </div>
                </div>
                <div>
                  <p className="text-sm text-gray-600">
                    Drag and drop the <strong>back side</strong> here, or{' '}
                    <label className="text-blue-600 hover:text-blue-800 cursor-pointer font-medium">
                      browse
                      <input
                        type="file"
                        className="sr-only"
                        accept="image/jpeg,image/jpg,image/png,image/bmp,image/tiff,application/pdf"
                        onChange={(e) => e.target.files?.[0] && handleFileSelect(e.target.files[0], 'idDocumentBack')}
                      />
                    </label>
                  </p>
                  <p className="text-xs text-gray-500 mt-1">
                    JPG, PNG, BMP, TIFF, PDF (Max 10MB)
                  </p>
                </div>
              </div>
            )}
          </div>
          {errors?.idDocumentBack && (
            <p className="mt-1 text-sm text-red-600">{errors.idDocumentBack}</p>
          )}
        </div>
      )}

      {/* Selfie Upload */}
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-2">
          Selfie Photo
        </label>
        <div
          className={`relative border-2 border-dashed rounded-lg p-6 text-center transition-colors ${
            dragActive ? 'border-blue-400 bg-blue-50' : 'border-gray-300'
          } ${errors?.selfieImage ? 'border-red-400 bg-red-50' : ''}`}
          onDragEnter={handleDrag}
          onDragLeave={handleDrag}
          onDragOver={handleDrag}
          onDrop={(e) => handleDrop(e, 'selfieImage')}
        >
          {selfieImage ? (
            <div className="space-y-4">
              <div className="flex items-center justify-center">
                <div className="bg-green-100 rounded-full p-3">
                  <svg className="h-8 w-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
              </div>
              <div className="flex items-center justify-center">
                <img
                  src={URL.createObjectURL(selfieImage)}
                  alt="Selfie preview"
                  className="h-24 w-24 object-cover rounded-full"
                />
              </div>
              <div>
                <p className="text-sm font-medium text-gray-900">{selfieImage.name}</p>
                <p className="text-xs text-gray-500">{(selfieImage.size / (1024 * 1024)).toFixed(2)} MB</p>
              </div>
              <button
                type="button"
                onClick={() => removeFile('selfieImage')}
                className="text-red-600 hover:text-red-800 text-sm font-medium"
              >
                Remove Photo
              </button>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="flex items-center justify-center">
                <div className="bg-gray-100 rounded-full p-3">
                  <svg className="h-8 w-8 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M14.828 14.828a4 4 0 01-5.656 0M9 10h.01M15 10h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
              </div>
              <div>
                <p className="text-sm text-gray-600">
                  Drag and drop your selfie here, or{' '}
                  <label className="text-blue-600 hover:text-blue-800 cursor-pointer font-medium">
                    browse
                    <input
                      type="file"
                      className="sr-only"
                      accept="image/jpeg,image/jpg,image/png,image/bmp"
                      onChange={(e) => e.target.files?.[0] && handleFileSelect(e.target.files[0], 'selfieImage')}
                    />
                  </label>
                </p>
                <p className="text-xs text-gray-500 mt-1">
                  Supported formats: JPG, PNG, BMP (Max 5MB)
                </p>
              </div>
            </div>
          )}
        </div>
        {errors?.selfieImage && (
          <p className="mt-1 text-sm text-red-600">{errors.selfieImage}</p>
        )}
      </div>

      {/* Upload Guidelines */}
      <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
        <h4 className="text-sm font-medium text-blue-800 mb-2">Upload Guidelines:</h4>
        <ul className="text-sm text-blue-700 space-y-1">
          <li>• Ensure documents are clear and well-lit</li>
          <li>• Avoid glare and shadows on ID documents</li>
          <li>• Selfie should show your full face clearly</li>
          <li>• Files should be in high resolution (300+ DPI recommended)</li>
          <li>• Make sure all text is readable and not blurry</li>
          {documentType === 'CitizenshipCard' && (
            <li>• <strong>Citizenship:</strong> Front = photo (used for face match). Back = English text (used for data extraction). Both improve verification.</li>
          )}
        </ul>
      </div>
    </div>
  );
}