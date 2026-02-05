'use client';

import { useState } from 'react';
import { VerificationStatus, DocumentType, Priority } from '@/types/shared';

interface VerificationFiltersProps {
  onFiltersChange: (filters: VerificationFilters) => void;
  onReset: () => void;
}

export interface VerificationFilters {
  searchTerm: string;
  status: string[];
  documentType: string[];
  priority: string[];
  dateRange: {
    startDate: string;
    endDate: string;
  } | null;
}

const statusOptions: { value: VerificationStatus; label: string }[] = [
  { value: 'Pending', label: 'Pending' },
  { value: 'Processing', label: 'Processing' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'ReviewNeeded', label: 'Review Needed' },
];

const documentTypeOptions: { value: DocumentType; label: string }[] = [
  { value: 'Passport', label: 'Passport' },
  { value: 'DriversLicense', label: "Driver's License" },
  { value: 'NationalID', label: 'National ID' },
  { value: 'CitizenshipCard', label: 'Citizenship Card' },
];

const priorityOptions: { value: Priority; label: string }[] = [
  { value: 'Low', label: 'Low' },
  { value: 'Normal', label: 'Normal' },
  { value: 'High', label: 'High' },
  { value: 'Urgent', label: 'Urgent' },
];

export default function VerificationFilters({ onFiltersChange, onReset }: VerificationFiltersProps) {
  const [filters, setFilters] = useState<VerificationFilters>({
    searchTerm: '',
    status: [],
    documentType: [],
    priority: [],
    dateRange: null,
  });

  const [showAdvanced, setShowAdvanced] = useState(false);

  const updateFilters = (updates: Partial<VerificationFilters>) => {
    const newFilters = { ...filters, ...updates };
    setFilters(newFilters);
    onFiltersChange(newFilters);
  };

  const handleStatusToggle = (status: VerificationStatus) => {
    const newStatus = filters.status.includes(status)
      ? filters.status.filter(s => s !== status)
      : [...filters.status, status];
    updateFilters({ status: newStatus });
  };

  const handleDocumentTypeToggle = (type: DocumentType) => {
    const newTypes = filters.documentType.includes(type)
      ? filters.documentType.filter(t => t !== type)
      : [...filters.documentType, type];
    updateFilters({ documentType: newTypes });
  };

  const handlePriorityToggle = (priority: Priority) => {
    const newPriorities = filters.priority.includes(priority)
      ? filters.priority.filter(p => p !== priority)
      : [...filters.priority, priority];
    updateFilters({ priority: newPriorities });
  };

  const hasActiveFilters = filters.searchTerm ||
    filters.status.length > 0 ||
    filters.documentType.length > 0 ||
    filters.priority.length > 0 ||
    filters.dateRange;

  return (
    <div className="bg-white rounded-lg shadow-sm border border-gray-200 p-6 mb-6">
      <div className="space-y-4">
        {/* Search */}
        <div>
          <label htmlFor="search" className="block text-sm font-medium text-gray-700 mb-1">
            Search
          </label>
          <input
            id="search"
            type="text"
            className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm text-gray-900 bg-white focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
            placeholder="Search by reference number, name, or email..."
            value={filters.searchTerm}
            onChange={(e) => updateFilters({ searchTerm: e.target.value })}
          />
        </div>

        {/* Advanced Filters Toggle */}
        <div className="flex justify-between items-center">
          <button
            type="button"
            onClick={() => setShowAdvanced(!showAdvanced)}
            className="text-sm text-blue-600 hover:text-blue-800 font-medium"
          >
            {showAdvanced ? 'Hide' : 'Show'} Advanced Filters
            {showAdvanced ? ' ▲' : ' ▼'}
          </button>
          {hasActiveFilters && (
            <button
              type="button"
              onClick={onReset}
              className="text-sm text-gray-600 hover:text-gray-800 font-medium"
            >
              Clear Filters
            </button>
          )}
        </div>

        {/* Advanced Filters */}
        {showAdvanced && (
          <div className="space-y-6 pt-4 border-t border-gray-200">
            {/* Status Filter */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Status</label>
              <div className="flex flex-wrap gap-2">
                {statusOptions.map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => handleStatusToggle(option.value)}
                    className={`px-3 py-1 rounded-full text-xs font-medium transition-colors ${
                      filters.status.includes(option.value)
                        ? 'bg-blue-100 text-blue-800'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </div>

            {/* Document Type Filter */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Document Type</label>
              <div className="flex flex-wrap gap-2">
                {documentTypeOptions.map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => handleDocumentTypeToggle(option.value)}
                    className={`px-3 py-1 rounded-full text-xs font-medium transition-colors ${
                      filters.documentType.includes(option.value)
                        ? 'bg-green-100 text-green-800'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </div>

            {/* Priority Filter */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Priority</label>
              <div className="flex flex-wrap gap-2">
                {priorityOptions.map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => handlePriorityToggle(option.value)}
                    className={`px-3 py-1 rounded-full text-xs font-medium transition-colors ${
                      filters.priority.includes(option.value)
                        ? 'bg-yellow-100 text-yellow-800'
                        : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                    }`}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </div>

            {/* Date Range Filter */}
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Date Range</label>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label htmlFor="startDate" className="block text-xs text-gray-500 mb-1">From</label>
                  <input
                    id="startDate"
                    type="date"
                    className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm text-gray-900 bg-white focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                    value={filters.dateRange?.startDate || ''}
                    onChange={(e) =>
                      updateFilters({
                        dateRange: {
                          startDate: e.target.value,
                          endDate: filters.dateRange?.endDate || '',
                        },
                      })
                    }
                  />
                </div>
                <div>
                  <label htmlFor="endDate" className="block text-xs text-gray-500 mb-1">To</label>
                  <input
                    id="endDate"
                    type="date"
                    className="block w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm text-gray-900 bg-white focus:outline-none focus:ring-blue-500 focus:border-blue-500 sm:text-sm"
                    value={filters.dateRange?.endDate || ''}
                    onChange={(e) =>
                      updateFilters({
                        dateRange: {
                          startDate: filters.dateRange?.startDate || '',
                          endDate: e.target.value,
                        },
                      })
                    }
                  />
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}