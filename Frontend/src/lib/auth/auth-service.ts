import { LoginRequest, LoginResponse, User, RefreshTokenRequest, RegisterRequest } from '@/types/shared';

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

class AuthService {
  private token: string | null = null;
  private refreshToken: string | null = null;

  constructor() {
    // Initialize tokens from localStorage
    if (typeof window !== 'undefined') {
      this.token = localStorage.getItem('auth_token');
      this.refreshToken = localStorage.getItem('refresh_token');
    }
  }

  async register(payload: RegisterRequest): Promise<LoginResponse> {
    const response = await fetch(`${API_BASE_URL}/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      throw new Error(errorData.error || 'Registration failed');
    }

    const data: LoginResponse = await response.json();
    this.setTokens(data.token, data.refreshToken);
    return data;
  }

  async login(credentials: LoginRequest): Promise<LoginResponse> {
    try {
      const response = await fetch(`${API_BASE_URL}/auth/login`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(credentials),
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.error || 'Login failed');
      }

      const data: LoginResponse = await response.json();

      // Store tokens
      this.setTokens(data.token, data.refreshToken);

      return data;
    } catch (error) {
      console.error('Login error:', error);
      throw error;
    }
  }

  async logout(): Promise<void> {
    try {
      if (this.token) {
        await fetch(`${API_BASE_URL}/auth/logout`, {
          method: 'POST',
          headers: {
            'Authorization': `Bearer ${this.token}`,
          },
        });
      }
    } catch (error) {
      console.error('Logout error:', error);
    } finally {
      // Clear tokens regardless of API call success
      this.clearTokens();
    }
  }

  async refreshAccessToken(): Promise<string | null> {
    try {
      if (!this.refreshToken) {
        throw new Error('No refresh token available');
      }

      const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ refreshToken: this.refreshToken }),
      });

      if (!response.ok) {
        throw new Error('Token refresh failed');
      }

      const data: LoginResponse = await response.json();

      // Update stored tokens
      this.setTokens(data.token, data.refreshToken);

      return data.token;
    } catch (error) {
      console.error('Token refresh error:', error);
      this.clearTokens();
      return null;
    }
  }

  async getCurrentUser(): Promise<User | null> {
    try {
      const token = await this.getValidToken();
      if (!token) {
        return null;
      }

      const response = await fetch(`${API_BASE_URL}/auth/profile`, {
        headers: {
          'Authorization': `Bearer ${token}`,
        },
      });

      if (!response.ok) {
        throw new Error('Failed to get user profile');
      }

      return await response.json();
    } catch (error) {
      console.error('Get current user error:', error);
      return null;
    }
  }

  async getValidToken(): Promise<string | null> {
    if (!this.token) {
      return null;
    }

    // Check if token is expired (simple check)
    try {
      const payload = JSON.parse(atob(this.token.split('.')[1]));
      const currentTime = Date.now() / 1000;

      if (payload.exp < currentTime) {
        // Token expired, try to refresh
        return await this.refreshAccessToken();
      }

      return this.token;
    } catch (error) {
      console.error('Token validation error:', error);
      return null;
    }
  }

  isAuthenticated(): boolean {
    return !!this.token;
  }

  getUserRoles(): string[] {
    if (!this.token) {
      return [];
    }

    try {
      const payload = JSON.parse(atob(this.token.split('.')[1]));
      return payload.roles || [];
    } catch (error) {
      console.error('Error parsing token:', error);
      return [];
    }
  }

  hasRole(role: string): boolean {
    const roles = this.getUserRoles();
    return roles.includes(role);
  }

  isAdmin(): boolean {
    return this.hasRole('Admin');
  }

  isVerificationOfficer(): boolean {
    return this.hasRole('VerificationOfficer');
  }

  private setTokens(token: string, refreshToken: string): void {
    this.token = token;
    this.refreshToken = refreshToken;

    if (typeof window !== 'undefined') {
      localStorage.setItem('auth_token', token);
      localStorage.setItem('refresh_token', refreshToken);
    }
  }

  private clearTokens(): void {
    this.token = null;
    this.refreshToken = null;

    if (typeof window !== 'undefined') {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('refresh_token');
    }
  }

  // Get authorization header for API calls
  getAuthHeader(): Record<string, string> {
    const token = this.token;
    return token ? { 'Authorization': `Bearer ${token}` } : {};
  }
}

export const authService = new AuthService();