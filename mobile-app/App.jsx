// TFY Cat Adventure — Expo (React Native) entry: Firebase connection + screen routes.
// Same trust model as the Unity game: anonymous sign-in, everything of value goes through Cloud Functions,
// and the player may only write their own `settings` directly (see firebase/firestore.rules).
import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, FlatList, Pressable, StyleSheet, Switch, Text, View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { getApps, initializeApp } from 'firebase/app';
import {
  connectAuthEmulator, getAuth, getReactNativePersistence, initializeAuth, onAuthStateChanged, signInAnonymously,
} from 'firebase/auth';
import {
  collection, connectFirestoreEmulator, doc, getDocs, getFirestore, onSnapshot, orderBy, query, updateDoc,
} from 'firebase/firestore';
import { connectFunctionsEmulator, getFunctions, httpsCallable } from 'firebase/functions';

// ─── Firebase ───────────────────────────────────────────────────────────────
// Set these in mobile-app/.env (Expo inlines EXPO_PUBLIC_* at build time).
// Emulator host: 10.0.2.2 = Android emulator → this PC; use the PC's LAN IP for a real phone.
const USE_EMULATORS = process.env.EXPO_PUBLIC_USE_EMULATORS === 'true';
const EMULATOR_HOST = process.env.EXPO_PUBLIC_EMULATOR_HOST || '10.0.2.2';

const firstInit = getApps().length === 0;
const app = firstInit
  ? initializeApp({
    apiKey: process.env.EXPO_PUBLIC_FIREBASE_API_KEY || 'demo-key',
    authDomain: process.env.EXPO_PUBLIC_FIREBASE_AUTH_DOMAIN,
    projectId: process.env.EXPO_PUBLIC_FIREBASE_PROJECT_ID || 'demo-tfy-cat',
    appId: process.env.EXPO_PUBLIC_FIREBASE_APP_ID,
  })
  : getApps()[0];

// Persist the anonymous account so a returning child keeps their cat, coins and progress.
const auth = firstInit
  ? initializeAuth(app, { persistence: getReactNativePersistence(AsyncStorage) })
  : getAuth(app);
const db = getFirestore(app);
const functions = getFunctions(app, process.env.EXPO_PUBLIC_FUNCTIONS_REGION || 'europe-west1');

if (USE_EMULATORS && firstInit) {
  connectAuthEmulator(auth, `http://${EMULATOR_HOST}:9099`, { disableWarnings: true });
  connectFirestoreEmulator(db, EMULATOR_HOST, 8080);
  connectFunctionsEmulator(functions, EMULATOR_HOST, 5001);
}

/** Callable Cloud Function → its returned data. */
const call = (name) => async (data) => (await httpsCallable(functions, name)(data ?? {})).data;
const api = {
  createPlayer: call('createPlayer'),
  getDailyReward: call('getDailyReward'),
  claimDailyReward: call('claimDailyReward'),
  deleteMyData: call('deleteMyData'),
};
const utcOffsetMinutes = () => -new Date().getTimezoneOffset();

// ─── Player session ─────────────────────────────────────────────────────────
// Boot flow: sign in anonymously → createPlayer (idempotent; server picks the username) → live profile.
const PlayerContext = createContext(null);
const usePlayer = () => useContext(PlayerContext);

function PlayerProvider({ children }) {
  const [uid, setUid] = useState(null);
  const [player, setPlayer] = useState(null);
  const [error, setError] = useState(null);

  const boot = useCallback(async () => {
    setError(null);
    try {
      const user = auth.currentUser ?? (await signInAnonymously(auth)).user;
      await api.createPlayer();
      setUid(user.uid);
    } catch (e) {
      console.warn('[TFY] boot failed', e?.code, e?.message);
      setError("Couldn't connect right now. Please try again.");
    }
  }, []);

  useEffect(() => onAuthStateChanged(auth, (user) => { if (!user) setUid(null); }), []);
  useEffect(() => { boot(); }, [boot]);

  // Coins, stars and progress change only on the server, so listen instead of caching.
  useEffect(() => {
    if (!uid) return undefined;
    return onSnapshot(doc(db, 'players', uid), (snap) => setPlayer(snap.exists() ? snap.data() : null),
      (e) => console.warn('[TFY] profile listener', e?.code));
  }, [uid]);

  const value = useMemo(() => ({ uid, player, error, retry: boot }), [uid, player, error, boot]);
  return <PlayerContext.Provider value={value}>{children}</PlayerContext.Provider>;
}

// ─── Screens ────────────────────────────────────────────────────────────────
function LoadingScreen() {
  const { error, retry } = usePlayer();
  return (
    <View style={styles.center}>
      <Text style={styles.title}>TFY Cat Adventure</Text>
      {error ? (
        <>
          <Text style={styles.muted}>{error}</Text>
          <BigButton label="Try again" onPress={retry} />
        </>
      ) : <ActivityIndicator size="large" />}
    </View>
  );
}

function HomeScreen({ navigation }) {
  const { player } = usePlayer();
  return (
    <View style={styles.center}>
      <Text style={styles.title}>Hi, {player.username}!</Text>
      <Text style={styles.muted}>🪙 {player.coins}   💎 {player.gems}   ⭐ {player.stars}   🐟 {player.fish}</Text>
      <BigButton label="PLAY" onPress={() => navigation.navigate('Levels')} />
      <BigButton label="Rewards" onPress={() => navigation.navigate('Rewards')} />
      <BigButton label="Settings" onPress={() => navigation.navigate('Settings')} />
    </View>
  );
}

function LevelsScreen() {
  const { player } = usePlayer();
  const [levels, setLevels] = useState(null);

  useEffect(() => {
    // Filter `enabled` on the device: enabled + orderBy(number) would need a composite index.
    getDocs(query(collection(db, 'levels'), orderBy('number')))
      .then((snap) => setLevels(snap.docs.map((d) => d.data()).filter((l) => l.enabled)))
      .catch((e) => { console.warn('[TFY] levels', e?.code); setLevels([]); });
  }, []);

  if (!levels) return <View style={styles.center}><ActivityIndicator size="large" /></View>;
  return (
    <FlatList
      data={levels}
      keyExtractor={(l) => l.id}
      contentContainerStyle={styles.list}
      renderItem={({ item }) => {
        const locked = item.number > player.currentLevel;
        const stars = player.levels?.[item.id]?.bestStars ?? 0;
        return (
          <View style={[styles.row, locked && styles.locked]}>
            <Text style={styles.rowTitle}>{locked ? '🔒 ' : ''}{item.number}. {item.name}</Text>
            <Text style={styles.muted}>{item.world} · {'⭐'.repeat(stars)}</Text>
          </View>
        );
      }}
    />
  );
}

function RewardsScreen() {
  const [daily, setDaily] = useState(null);
  const [message, setMessage] = useState('');

  const load = useCallback(() => {
    api.getDailyReward({ utcOffsetMinutes: utcOffsetMinutes() }).then(setDaily)
      .catch(() => setMessage("Couldn't load rewards right now."));
  }, []);
  useEffect(load, [load]);

  const claim = async () => {
    try {
      await api.claimDailyReward({ utcOffsetMinutes: utcOffsetMinutes() });
      setMessage('Yay! Treat collected 🎉');
      load();
    } catch (e) {
      setMessage(e?.message || 'Come back tomorrow for another treat!');
    }
  };

  return (
    <View style={styles.center}>
      <Text style={styles.title}>Daily treats</Text>
      {!daily ? <ActivityIndicator /> : daily.enabled
        ? <BigButton label="Collect today's treat" onPress={claim} />
        : <Text style={styles.muted}>Daily treats are resting today.</Text>}
      {!!message && <Text style={styles.muted}>{message}</Text>}
    </View>
  );
}

function SettingsScreen() {
  const { uid, player } = usePlayer();
  const settings = player.settings;

  // Rules require the full settings map plus an integer updatedAt; nothing else may change.
  const toggle = (key) => updateDoc(doc(db, 'players', uid), {
    settings: { ...settings, [key]: !settings[key] },
    updatedAt: Date.now(),
  }).catch((e) => console.warn('[TFY] settings', e?.code));

  return (
    <View style={styles.list}>
      {['music', 'sound', 'vibration', 'reduceMotion'].map((key) => (
        <View key={key} style={styles.row}>
          <Text style={styles.rowTitle}>{SETTING_LABELS[key]}</Text>
          <Switch value={!!settings[key]} onValueChange={() => toggle(key)} />
        </View>
      ))}
    </View>
  );
}
const SETTING_LABELS = { music: 'Music', sound: 'Sound', vibration: 'Vibration', reduceMotion: 'Calm camera' };

function BigButton({ label, onPress }) {
  return (
    <Pressable onPress={onPress} style={({ pressed }) => [styles.button, pressed && { opacity: 0.8 }]}>
      <Text style={styles.buttonText}>{label}</Text>
    </Pressable>
  );
}

// ─── Routes ─────────────────────────────────────────────────────────────────
const Stack = createNativeStackNavigator();

function Routes() {
  const { player } = usePlayer();
  return (
    <Stack.Navigator screenOptions={{ headerTintColor: '#7a3cff' }}>
      {!player ? (
        <Stack.Screen name="Loading" component={LoadingScreen} options={{ headerShown: false }} />
      ) : (
        <>
          <Stack.Screen name="Home" component={HomeScreen} options={{ headerShown: false }} />
          <Stack.Screen name="Levels" component={LevelsScreen} />
          <Stack.Screen name="Rewards" component={RewardsScreen} />
          <Stack.Screen name="Settings" component={SettingsScreen} />
        </>
      )}
    </Stack.Navigator>
  );
}

export default function App() {
  return (
    <PlayerProvider>
      <NavigationContainer>
        <Routes />
      </NavigationContainer>
    </PlayerProvider>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', gap: 14, padding: 24, backgroundColor: '#fff8ec' },
  title: { fontSize: 28, fontWeight: '800', color: '#3b2a5a' },
  muted: { fontSize: 16, color: '#6b5f80', textAlign: 'center' },
  button: { minWidth: 220, paddingVertical: 16, borderRadius: 24, backgroundColor: '#7a3cff', alignItems: 'center' },
  buttonText: { color: '#fff', fontSize: 20, fontWeight: '800' },
  list: { padding: 16, gap: 10 },
  row: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: 16, borderRadius: 16, backgroundColor: '#fff' },
  rowTitle: { fontSize: 17, fontWeight: '700', color: '#3b2a5a' },
  locked: { opacity: 0.5 },
});
