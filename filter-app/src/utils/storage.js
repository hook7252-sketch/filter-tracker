import AsyncStorage from '@react-native-async-storage/async-storage';
import { INITIAL_ITEMS } from '../data/initialData';

const STORAGE_KEY = '@filter_tracker_items';

export async function loadItems() {
  try {
    const json = await AsyncStorage.getItem(STORAGE_KEY);
    if (json) return JSON.parse(json);
    return INITIAL_ITEMS;
  } catch {
    return INITIAL_ITEMS;
  }
}

export async function saveItems(items) {
  try {
    await AsyncStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  } catch (e) {
    console.error('Failed to save items:', e);
  }
}
