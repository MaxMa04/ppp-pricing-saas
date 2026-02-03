"use client";

import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { onAuthStateChange, type User } from "./auth";

interface AuthContextType {
  user: User | null;
  loading: boolean;
  internalUserId: string | null;
}

const AuthContext = createContext<AuthContextType>({
  user: null,
  loading: true,
  internalUserId: null,
});

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [internalUserId, setInternalUserId] = useState<string | null>(null);

  useEffect(() => {
    const unsubscribe = onAuthStateChange(async (firebaseUser) => {
      setUser(firebaseUser);

      if (firebaseUser) {
        const token = await firebaseUser.getIdToken();
        try {
          const response = await fetch(
            `${process.env.NEXT_PUBLIC_API_URL}/api/auth/verify`,
            {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
                Authorization: `Bearer ${token}`,
              },
            }
          );
          if (response.ok) {
            const data = await response.json();
            setInternalUserId(data.id);
            localStorage.setItem("internalUserId", data.id);
          }
        } catch (error) {
          console.error("Failed to sync user with backend:", error instanceof Error ? error.message : "Unknown error");
        }
      } else {
        setInternalUserId(null);
        localStorage.removeItem("internalUserId");
      }

      setLoading(false);
    });

    return () => unsubscribe();
  }, []);

  return (
    <AuthContext.Provider value={{ user, loading, internalUserId }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
