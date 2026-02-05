import { VerificationUpdateMessage, NotificationMessage, WebSocketMessage } from '@/types/shared';

type EventHandler = (message: WebSocketMessage) => void;
type ConnectionStatus = 'connecting' | 'connected' | 'disconnected' | 'error';

class WebSocketService {
  private ws: WebSocket | null = null;
  private url: string;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private reconnectDelay = 1000;
  private eventHandlers: Map<string, EventHandler[]> = new Map();
  private connectionStatus: ConnectionStatus = 'disconnected';
  private statusHandlers: ((status: ConnectionStatus) => void)[] = [];

  constructor() {
    // Use ws:// for local development, wss:// for production
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const host = window.location.host;
    this.url = `${protocol}//${host}/ws`;
  }

  connect(): Promise<void> {
    return new Promise((resolve, reject) => {
      if (this.ws?.readyState === WebSocket.OPEN) {
        resolve();
        return;
      }

      this.setConnectionStatus('connecting');

      try {
        this.ws = new WebSocket(this.url);

        this.ws.onopen = () => {
          this.setConnectionStatus('connected');
          this.reconnectAttempts = 0;
          console.log('WebSocket connected');
          resolve();
        };

        this.ws.onmessage = (event) => {
          try {
            const message: WebSocketMessage = JSON.parse(event.data);
            this.handleMessage(message);
          } catch (error) {
            console.error('Error parsing WebSocket message:', error);
          }
        };

        this.ws.onclose = (event) => {
          this.setConnectionStatus('disconnected');
          console.log('WebSocket disconnected:', event.code, event.reason);

          // Attempt to reconnect if not a normal closure
          if (event.code !== 1000 && this.reconnectAttempts < this.maxReconnectAttempts) {
            this.scheduleReconnect();
          }
        };

        this.ws.onerror = (error) => {
          this.setConnectionStatus('error');
          console.error('WebSocket error:', error);
          reject(error);
        };

      } catch (error) {
        this.setConnectionStatus('error');
        reject(error);
      }
    });
  }

  disconnect(): void {
    this.reconnectAttempts = this.maxReconnectAttempts; // Prevent reconnection
    if (this.ws) {
      this.ws.close(1000, 'User disconnect');
      this.ws = null;
    }
    this.setConnectionStatus('disconnected');
  }

  private setConnectionStatus(status: ConnectionStatus): void {
    this.connectionStatus = status;
    this.statusHandlers.forEach(handler => handler(status));
  }

  private scheduleReconnect(): void {
    this.reconnectAttempts++;
    const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1);

    console.log(`Attempting to reconnect in ${delay}ms (attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})`);

    setTimeout(() => {
      this.connect().catch(() => {
        // Reconnection failed, will be retried if within limits
      });
    }, delay);
  }

  private handleMessage(message: WebSocketMessage): void {
    const handlers = this.eventHandlers.get(message.type) || [];
    handlers.forEach(handler => handler(message));
  }

  // Event subscription methods
  onVerificationUpdate(handler: (message: VerificationUpdateMessage) => void): () => void {
    return this.addEventListener('verification_update', handler);
  }

  onNotification(handler: (message: NotificationMessage) => void): () => void {
    return this.addEventListener('notification', handler);
  }

  onConnectionStatusChange(handler: (status: ConnectionStatus) => void): () => void {
    this.statusHandlers.push(handler);
    return () => {
      const index = this.statusHandlers.indexOf(handler);
      if (index > -1) {
        this.statusHandlers.splice(index, 1);
      }
    };
  }

  private addEventListener(eventType: string, handler: EventHandler): () => void {
    if (!this.eventHandlers.has(eventType)) {
      this.eventHandlers.set(eventType, []);
    }
    this.eventHandlers.get(eventType)!.push(handler);

    // Return unsubscribe function
    return () => {
      const handlers = this.eventHandlers.get(eventType);
      if (handlers) {
        const index = handlers.indexOf(handler);
        if (index > -1) {
          handlers.splice(index, 1);
        }
      }
    };
  }

  // Send message to server
  sendVerificationRoom(verificationId: string): void {
    this.send({
      type: 'join_verification',
      data: { verificationId },
      timestamp: new Date().toISOString(),
      id: this.generateId(),
    });
  }

  leaveVerificationRoom(verificationId: string): void {
    this.send({
      type: 'leave_verification',
      data: { verificationId },
      timestamp: new Date().toISOString(),
      id: this.generateId(),
    });
  }

  private send(message: WebSocketMessage): void {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(message));
    } else {
      console.warn('WebSocket not connected, message not sent:', message);
    }
  }

  private generateId(): string {
    return Math.random().toString(36).substr(2, 9);
  }

  // Get current connection status
  getConnectionStatus(): ConnectionStatus {
    return this.connectionStatus;
  }

  // Check if WebSocket is connected
  isConnected(): boolean {
    return this.connectionStatus === 'connected' && this.ws?.readyState === WebSocket.OPEN;
  }
}

// Create singleton instance
export const webSocketService = new WebSocketService();

// React hook for WebSocket connection
export function useWebSocketConnection(autoConnect = true) {
  const [connectionStatus, setConnectionStatus] = useState(webSocketService.getConnectionStatus());

  useEffect(() => {
    if (autoConnect) {
      webSocketService.connect().catch(console.error);
    }

    const unsubscribe = webSocketService.onConnectionStatusChange(setConnectionStatus);

    return () => {
      unsubscribe();
      webSocketService.disconnect();
    };
  }, [autoConnect]);

  return connectionStatus;
}

// React hook for verification updates
export function useVerificationUpdates(verificationId?: string) {
  const [lastUpdate, setLastUpdate] = useState<VerificationUpdateMessage | null>(null);

  useEffect(() => {
    if (!verificationId || !webSocketService.isConnected()) {
      return;
    }

    // Join verification room
    webSocketService.sendVerificationRoom(verificationId);

    // Listen for updates
    const unsubscribe = webSocketService.onVerificationUpdate((message) => {
      if (message.data.verificationId === verificationId) {
        setLastUpdate(message);
      }
    });

    return () => {
      unsubscribe();
      webSocketService.leaveVerificationRoom(verificationId);
    };
  }, [verificationId]);

  return lastUpdate;
}

// React hook for notifications
export function useNotifications() {
  const [notifications, setNotifications] = useState<NotificationMessage[]>([]);

  useEffect(() => {
    if (!webSocketService.isConnected()) {
      return;
    }

    const unsubscribe = webSocketService.onNotification((message) => {
      setNotifications(prev => [...prev, message]);

      // Auto-remove notification after 5 seconds
      setTimeout(() => {
        setNotifications(prev => prev.filter(n => n.id !== message.id));
      }, 5000);
    });

    return unsubscribe;
  }, []);

  const clearNotifications = () => {
    setNotifications([]);
  };

  const removeNotification = (id: string) => {
    setNotifications(prev => prev.filter(n => n.id !== id));
  };

  return {
    notifications,
    clearNotifications,
    removeNotification,
  };
}