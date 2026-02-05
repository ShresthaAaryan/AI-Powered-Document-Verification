'use client';

import { useState, useEffect } from 'react';
import DashboardLayout from '@/components/layout/dashboard-layout';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

interface SystemSettings {
  maxFileSize: number;
  allowedFileTypes: string[];
  faceMatchingThreshold: number;
  authenticityThresholds: {
    genuine: number;
    suspicious: number;
  };
  ocrSettings: {
    languages: string[];
    confidenceThreshold: number;
  };
  notificationSettings: {
    emailEnabled: boolean;
    webhookUrl?: string;
  };
}

export default function SystemSettings() {
  const [settings, setSettings] = useState<SystemSettings>({
    maxFileSize: 10,
    allowedFileTypes: ['jpg', 'jpeg', 'png', 'bmp', 'tiff', 'pdf'],
    faceMatchingThreshold: 0.6,
    authenticityThresholds: {
      genuine: 80,
      suspicious: 50,
    },
    ocrSettings: {
      languages: ['eng', 'spa', 'fra', 'deu'],
      confidenceThreshold: 0.7,
    },
    notificationSettings: {
      emailEnabled: true,
      webhookUrl: '',
    },
  });
  const [isLoading, setIsLoading] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const handleInputChange = (field: string, value: any) => {
    setSettings(prev => ({
      ...prev,
      [field]: value,
    }));
  };

  const handleNestedChange = (parent: string, field: string, value: any) => {
    setSettings(prev => ({
      ...prev,
      [parent]: {
        ...(prev as any)[parent],
        [field]: value,
      },
    }));
  };

  const handleSave = async () => {
    setIsLoading(true);
    setMessage(null);

    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1000));
      setMessage({ type: 'success', text: 'Settings saved successfully' });
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to save settings' });
    } finally {
      setIsLoading(false);
    }
  };

  const handleReset = () => {
    // Reset to default values
    setSettings({
      maxFileSize: 10,
      allowedFileTypes: ['jpg', 'jpeg', 'png', 'bmp', 'tiff', 'pdf'],
      faceMatchingThreshold: 0.6,
      authenticityThresholds: {
        genuine: 80,
        suspicious: 50,
      },
      ocrSettings: {
        languages: ['eng', 'spa', 'fra', 'deu'],
        confidenceThreshold: 0.7,
      },
      notificationSettings: {
        emailEnabled: true,
        webhookUrl: '',
      },
    });
    setMessage({ type: 'success', text: 'Settings reset to defaults' });
  };

  return (
    <DashboardLayout>
      <div className="max-w-4xl mx-auto p-6">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900">System Settings</h1>
          <p className="mt-2 text-gray-600">
            Configure system parameters and AI model thresholds
          </p>
        </div>

        {/* Message */}
        {message && (
          <div className={`mb-6 p-4 rounded-md ${
            message.type === 'success'
              ? 'bg-green-50 border border-green-200'
              : 'bg-red-50 border border-red-200'
          }`}>
            <div className={`text-sm ${
              message.type === 'success' ? 'text-green-600' : 'text-red-600'
            }`}>
              {message.text}
            </div>
          </div>
        )}

        {/* Settings Form */}
        <div className="space-y-8">
          {/* File Upload Settings */}
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-lg font-medium text-gray-900 mb-4">File Upload Settings</h2>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Maximum File Size (MB)
                </label>
                <Input
                  type="number"
                  min="1"
                  max="100"
                  value={settings.maxFileSize}
                  onChange={(e) => handleInputChange('maxFileSize', parseInt(e.target.value))}
                />
                <p className="mt-1 text-xs text-gray-500">Maximum size for uploaded files</p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Allowed File Types
                </label>
                <div className="flex flex-wrap gap-2 mt-2">
                  {settings.allowedFileTypes.map((type, index) => (
                    <span
                      key={index}
                      className="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium bg-gray-100 text-gray-800"
                    >
                      .{type}
                    </span>
                  ))}
                </div>
                <p className="mt-1 text-xs text-gray-500">Supported file formats</p>
              </div>
            </div>
          </div>

          {/* AI Model Settings */}
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-lg font-medium text-gray-900 mb-4">AI Model Settings</h2>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Face Matching Threshold
                </label>
                <Input
                  type="number"
                  min="0"
                  max="1"
                  step="0.01"
                  value={settings.faceMatchingThreshold}
                  onChange={(e) => handleInputChange('faceMatchingThreshold', parseFloat(e.target.value))}
                />
                <p className="mt-1 text-xs text-gray-500">
                  Minimum similarity score for face matching (0.0 - 1.0)
                </p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  Authenticity Thresholds
                </label>
                <div className="space-y-2">
                  <div>
                    <label className="block text-xs text-gray-500">Genuine (minimum score)</label>
                    <Input
                      type="number"
                      min="0"
                      max="100"
                      value={settings.authenticityThresholds.genuine}
                      onChange={(e) => handleNestedChange('authenticityThresholds', 'genuine', parseInt(e.target.value))}
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-500">Suspicious (minimum score)</label>
                    <Input
                      type="number"
                      min="0"
                      max="100"
                      value={settings.authenticityThresholds.suspicious}
                      onChange={(e) => handleNestedChange('authenticityThresholds', 'suspicious', parseInt(e.target.value))}
                    />
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* OCR Settings */}
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-lg font-medium text-gray-900 mb-4">OCR Settings</h2>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Supported Languages
                </label>
                <div className="flex flex-wrap gap-2 mt-2">
                  {settings.ocrSettings.languages.map((lang, index) => (
                    <span
                      key={index}
                      className="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium bg-blue-100 text-blue-800"
                    >
                      {lang}
                    </span>
                  ))}
                </div>
                <p className="mt-1 text-xs text-gray-500">OCR language codes</p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Confidence Threshold
                </label>
                <Input
                  type="number"
                  min="0"
                  max="1"
                  step="0.01"
                  value={settings.ocrSettings.confidenceThreshold}
                  onChange={(e) => handleNestedChange('ocrSettings', 'confidenceThreshold', parseFloat(e.target.value))}
                />
                <p className="mt-1 text-xs text-gray-500">
                  Minimum confidence for OCR extraction (0.0 - 1.0)
                </p>
              </div>
            </div>
          </div>

          {/* Notification Settings */}
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-lg font-medium text-gray-900 mb-4">Notification Settings</h2>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <label className="flex items-center">
                  <input
                    type="checkbox"
                    checked={settings.notificationSettings.emailEnabled}
                    onChange={(e) => handleNestedChange('notificationSettings', 'emailEnabled', e.target.checked)}
                    className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                  />
                  <span className="ml-2 text-sm font-medium text-gray-700">
                    Enable Email Notifications
                  </span>
                </label>
                <p className="mt-1 text-xs text-gray-500">Send email alerts for verification updates</p>
              </div>

              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">
                  Webhook URL
                </label>
                <Input
                  type="url"
                  placeholder="https://your-webhook-url.com"
                  value={settings.notificationSettings.webhookUrl || ''}
                  onChange={(e) => handleNestedChange('notificationSettings', 'webhookUrl', e.target.value)}
                />
                <p className="mt-1 text-xs text-gray-500">Optional webhook for real-time notifications</p>
              </div>
            </div>
          </div>

          {/* System Information */}
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-lg font-medium text-gray-900 mb-4">System Information</h2>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              <div>
                <h3 className="text-sm font-medium text-gray-700 mb-2">Backend Version</h3>
                <p className="text-sm text-gray-600">1.0.0</p>
              </div>
              <div>
                <h3 className="text-sm font-medium text-gray-700 mb-2">Tesseract Version</h3>
                <p className="text-sm text-gray-600">5.3.1</p>
              </div>
              <div>
                <h3 className="text-sm font-medium text-gray-700 mb-2">ONNX Runtime</h3>
                <p className="text-sm text-gray-600">1.16.3</p>
              </div>
            </div>
          </div>

          {/* Action Buttons */}
          <div className="flex justify-end space-x-4">
            <Button
              variant="outline"
              onClick={handleReset}
              disabled={isLoading}
            >
              Reset to Defaults
            </Button>
            <Button
              onClick={handleSave}
              disabled={isLoading}
              isLoading={isLoading}
            >
              Save Settings
            </Button>
          </div>
        </div>
      </div>
    </DashboardLayout>
  );
}