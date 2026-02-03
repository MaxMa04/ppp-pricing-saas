import {
  signInWithPopup,
  signInWithEmailAndPassword,
  createUserWithEmailAndPassword,
  signOut as firebaseSignOut,
  GoogleAuthProvider,
  onAuthStateChanged,
  type User,
  updateProfile,
  type Auth,
} from "firebase/auth";
import { auth } from "./config";

const googleProvider = new GoogleAuthProvider();

function getAuth(): Auth {
  if (!auth) {
    throw new Error("Firebase auth is not initialized");
  }
  return auth;
}

export async function signInWithGoogle() {
  const result = await signInWithPopup(getAuth(), googleProvider);
  const idToken = await result.user.getIdToken();
  return { user: result.user, idToken };
}

export async function signInWithEmail(email: string, password: string) {
  const result = await signInWithEmailAndPassword(getAuth(), email, password);
  const idToken = await result.user.getIdToken();
  return { user: result.user, idToken };
}

export async function signUpWithEmail(
  email: string,
  password: string,
  displayName?: string
) {
  const result = await createUserWithEmailAndPassword(getAuth(), email, password);
  if (displayName) {
    await updateProfile(result.user, { displayName });
  }
  const idToken = await result.user.getIdToken();
  return { user: result.user, idToken };
}

export async function signOut() {
  await firebaseSignOut(getAuth());
}

export async function getCurrentUserToken(): Promise<string | null> {
  const firebaseAuth = auth;
  if (!firebaseAuth) return null;
  const user = firebaseAuth.currentUser;
  if (!user) return null;
  return user.getIdToken();
}

export function onAuthStateChange(callback: (user: User | null) => void) {
  const firebaseAuth = auth;
  if (!firebaseAuth) {
    // Return a no-op unsubscribe function
    return () => {};
  }
  return onAuthStateChanged(firebaseAuth, callback);
}

export type { User };
