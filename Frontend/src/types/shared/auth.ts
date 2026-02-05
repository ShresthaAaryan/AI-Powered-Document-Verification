export interface LoginRequest {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  token: string;
  refreshToken: string;
  expiration: string;
  user: User;
}

export interface User {
  id: string;
  email: string;
  userName: string;
  roles: string[];
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface ValidateTokenRequest {
  token: string;
}

export interface UserProfile {
  id: string;
  email: string;
  userName: string;
  firstName?: string;
  lastName?: string;
  roles: string[];
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
  firstName?: string;
  lastName?: string;
}

export interface UpdateProfileRequest {
  firstName?: string;
  lastName?: string;
  currentPassword?: string;
  newPassword?: string;
}