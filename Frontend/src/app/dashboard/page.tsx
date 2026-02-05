'use client';

import { useState, useEffect } from 'react';
import DashboardLayout from '@/components/layout/dashboard-layout';
import { verificationService } from '@/lib/api/verification-service';

export default function DashboardPage() {
  const [stats, setStats] = useState<any>(null);
  const [recentVerifications, setRecentVerifications] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const loadDashboardData = async () => {
      try {
        const [statsData, verificationsData] = await Promise.all([
          verificationService.getWorkflowStats(),
          verificationService.getMyVerifications({ page: 1, pageSize: 5 })
        ]);

        setStats(statsData);
        setRecentVerifications(verificationsData.verifications);
      } catch (error) {
        console.error('Failed to load dashboard data:', error);
      } finally {
        setIsLoading(false);
      }
    };

    loadDashboardData();
  }, []);

  if (isLoading) {
    return (
      <DashboardLayout>
        <div className="p-6">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-200 rounded w-1/3 mb-6"></div>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
              {[1, 2, 3, 4].map((i) => (
                <div key={i} className="bg-white p-6 rounded-lg shadow">
                  <div className="h-6 bg-gray-200 rounded w-1/2 mb-2"></div>
                  <div className="h-8 bg-gray-200 rounded w-3/4"></div>
                </div>
              ))}
            </div>
            <div className="bg-white rounded-lg shadow p-6">
              <div className="h-6 bg-gray-200 rounded w-1/3 mb-4"></div>
              <div className="space-y-3">
                {[1, 2, 3, 4, 5].map((i) => (
                  <div key={i} className="h-12 bg-gray-200 rounded"></div>
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
      <div className="p-6">
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900">Dashboard</h1>
          <p className="mt-2 text-gray-600">
            Welcome to your Document Verification dashboard
          </p>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <div className="bg-white p-6 rounded-lg shadow">
            <div className="flex items-center">
              <div className="flex-shrink-0 bg-blue-100 rounded-md p-3">
                <div className="text-blue-600 text-xl font-bold">📋</div>
              </div>
              <div className="ml-4">
                <p className="text-sm font-medium text-gray-600">Total Verifications</p>
                <p className="text-2xl font-bold text-gray-900">{stats?.totalVerifications || 0}</p>
              </div>
            </div>
          </div>

          <div className="bg-white p-6 rounded-lg shadow">
            <div className="flex items-center">
              <div className="flex-shrink-0 bg-yellow-100 rounded-md p-3">
                <div className="text-yellow-600 text-xl font-bold">⏳</div>
              </div>
              <div className="ml-4">
                <p className="text-sm font-medium text-gray-600">Pending</p>
                <p className="text-2xl font-bold text-gray-900">{stats?.pendingVerifications || 0}</p>
              </div>
            </div>
          </div>

          <div className="bg-white p-6 rounded-lg shadow">
            <div className="flex items-center">
              <div className="flex-shrink-0 bg-green-100 rounded-md p-3">
                <div className="text-green-600 text-xl font-bold">✓</div>
              </div>
              <div className="ml-4">
                <p className="text-sm font-medium text-gray-600">Approved</p>
                <p className="text-2xl font-bold text-gray-900">{stats?.approvedVerifications || 0}</p>
              </div>
            </div>
          </div>

          <div className="bg-white p-6 rounded-lg shadow">
            <div className="flex items-center">
              <div className="flex-shrink-0 bg-blue-100 rounded-md p-3">
                <div className="text-blue-600 text-xl font-bold">📊</div>
              </div>
              <div className="ml-4">
                <p className="text-sm font-medium text-gray-600">Today</p>
                <p className="text-2xl font-bold text-gray-900">{stats?.todayVerifications || 0}</p>
              </div>
            </div>
          </div>
        </div>

        {/* Recent Verifications */}
        <div className="bg-white rounded-lg shadow">
          <div className="px-6 py-4 border-b border-gray-200">
            <h2 className="text-lg font-medium text-gray-900">Recent Verifications</h2>
          </div>
          <div className="overflow-hidden">
            {recentVerifications.length > 0 ? (
              <ul className="divide-y divide-gray-200">
                {recentVerifications.map((verification) => (
                  <li key={verification.id} className="px-6 py-4">
                    <div className="flex items-center justify-between">
                      <div className="flex-1">
                        <p className="text-sm font-medium text-gray-900">
                          {verification.referenceNumber}
                        </p>
                        <p className="text-sm text-gray-600">
                          {verification.documentType} • {verification.createdAt}
                        </p>
                      </div>
                      <div className="flex items-center space-x-2">
                        <span className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium ${verificationService.getStatusColor(verification.status)}`}>
                          {verificationService.getStatusIcon(verification.status)} {verification.status}
                        </span>
                        <a
                          href={`/verify/${verification.id}/status`}
                          className="text-blue-600 hover:text-blue-900 text-sm font-medium"
                        >
                          View →
                        </a>
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            ) : (
              <div className="px-6 py-12 text-center">
                <div className="text-gray-400 text-4xl mb-4">📄</div>
                <p className="text-gray-600">No verifications yet</p>
                <p className="text-gray-500 text-sm mt-1">Start by creating your first verification</p>
                <a
                  href="/verify/new"
                  className="mt-4 inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700"
                >
                  Create Verification
                </a>
              </div>
            )}
          </div>
        </div>

        {/* Quick Actions */}
        <div className="mt-8">
          <h2 className="text-lg font-medium text-gray-900 mb-4">Quick Actions</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <a
              href="/verify/new"
              className="flex items-center p-4 bg-white rounded-lg shadow hover:shadow-md transition-shadow"
            >
              <div className="flex-shrink-0 bg-blue-100 rounded-md p-3 mr-4">
                <span className="text-blue-600 text-xl">📤</span>
              </div>
              <div>
                <h3 className="text-sm font-medium text-gray-900">New Verification</h3>
                <p className="text-sm text-gray-600">Upload documents for verification</p>
              </div>
            </a>

            <a
              href="/verify/history"
              className="flex items-center p-4 bg-white rounded-lg shadow hover:shadow-md transition-shadow"
            >
              <div className="flex-shrink-0 bg-green-100 rounded-md p-3 mr-4">
                <span className="text-green-600 text-xl">📊</span>
              </div>
              <div>
                <h3 className="text-sm font-medium text-gray-900">View History</h3>
                <p className="text-sm text-gray-600">Browse all your verifications</p>
              </div>
            </a>

            <a
              href="/review"
              className="flex items-center p-4 bg-white rounded-lg shadow hover:shadow-md transition-shadow"
            >
              <div className="flex-shrink-0 bg-yellow-100 rounded-md p-3 mr-4">
                <span className="text-yellow-600 text-xl">👁️</span>
              </div>
              <div>
                <h3 className="text-sm font-medium text-gray-900">Review Queue</h3>
                <p className="text-sm text-gray-600">Review pending verifications</p>
              </div>
            </a>
          </div>
        </div>
      </div>
    </DashboardLayout>
  );
}