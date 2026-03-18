import React, { useState, useEffect } from 'react';
import {
  View, Text, TextInput, ScrollView, TouchableOpacity,
  StyleSheet, Alert, KeyboardAvoidingView, Platform,
} from 'react-native';
import { loadItems, saveItems } from '../utils/storage';
import { CATEGORIES, LOCATIONS } from '../data/initialData';

export default function EditScreen({ route, navigation }) {
  const { itemId } = route.params;
  const isNew = !itemId;

  const [form, setForm] = useState({
    category: '공기청정기',
    name: '',
    location: '거실',
    replacementCycleDays: '180',
    lastReplacedDate: '',
    price: '',
    link: '',
    note: '',
  });

  useEffect(() => {
    if (!isNew) {
      loadItems().then(items => {
        const found = items.find(i => i.id === itemId);
        if (found) {
          setForm({
            category: found.category,
            name: found.name,
            location: found.location,
            replacementCycleDays: String(found.replacementCycleDays),
            lastReplacedDate: found.lastReplacedDate || '',
            price: found.price || '',
            link: found.link || '',
            note: found.note || '',
          });
        }
      });
    }
    navigation.setOptions({ title: isNew ? '새 소모품 추가' : '수정' });
  }, [itemId]);

  const set = (key, value) => setForm(prev => ({ ...prev, [key]: value }));

  const validate = () => {
    if (!form.name.trim()) { Alert.alert('오류', '제품명을 입력해주세요.'); return false; }
    const cycle = parseInt(form.replacementCycleDays, 10);
    if (isNaN(cycle) || cycle <= 0) { Alert.alert('오류', '교환주기를 올바르게 입력해주세요.'); return false; }
    if (form.lastReplacedDate && !/^\d{4}-\d{2}-\d{2}$/.test(form.lastReplacedDate)) {
      Alert.alert('오류', '날짜 형식은 YYYY-MM-DD 입니다.'); return false;
    }
    return true;
  };

  const handleSave = async () => {
    if (!validate()) return;
    const items = await loadItems();
    if (isNew) {
      const newItem = {
        ...form,
        id: Date.now().toString(),
        replacementCycleDays: parseInt(form.replacementCycleDays, 10),
        lastReplacedDate: form.lastReplacedDate || null,
      };
      await saveItems([...items, newItem]);
      navigation.goBack();
    } else {
      const updated = items.map(i =>
        i.id === itemId
          ? {
              ...i, ...form,
              replacementCycleDays: parseInt(form.replacementCycleDays, 10),
              lastReplacedDate: form.lastReplacedDate || null,
            }
          : i
      );
      await saveItems(updated);
      navigation.goBack();
    }
  };

  return (
    <KeyboardAvoidingView
      style={{ flex: 1 }}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView style={styles.container} contentContainerStyle={styles.content}>

        <Label text="카테고리" />
        <SelectRow
          options={CATEGORIES}
          selected={form.category}
          onSelect={v => set('category', v)}
        />

        <Label text="제품명 *" />
        <TextInput
          style={styles.input}
          value={form.name}
          onChangeText={v => set('name', v)}
          placeholder="예: 위닉스 타워프라임 플러스"
          placeholderTextColor="#C7C7CC"
        />

        <Label text="장소" />
        <SelectRow
          options={LOCATIONS}
          selected={form.location}
          onSelect={v => set('location', v)}
        />

        <Label text="교환주기 (일) *" />
        <TextInput
          style={styles.input}
          value={form.replacementCycleDays}
          onChangeText={v => set('replacementCycleDays', v)}
          keyboardType="number-pad"
          placeholder="예: 180"
          placeholderTextColor="#C7C7CC"
        />

        <Label text="최근 교환일 (YYYY-MM-DD)" />
        <TextInput
          style={styles.input}
          value={form.lastReplacedDate}
          onChangeText={v => set('lastReplacedDate', v)}
          placeholder="예: 2025-01-15"
          placeholderTextColor="#C7C7CC"
          keyboardType="numbers-and-punctuation"
        />

        <Label text="구입처 가격" />
        <TextInput
          style={styles.input}
          value={form.price}
          onChangeText={v => set('price', v)}
          placeholder="예: 35,000원"
          placeholderTextColor="#C7C7CC"
        />

        <Label text="구매 링크" />
        <TextInput
          style={styles.input}
          value={form.link}
          onChangeText={v => set('link', v)}
          placeholder="https://..."
          placeholderTextColor="#C7C7CC"
          autoCapitalize="none"
          keyboardType="url"
        />

        <Label text="메모" />
        <TextInput
          style={[styles.input, styles.textarea]}
          value={form.note}
          onChangeText={v => set('note', v)}
          placeholder="참고사항..."
          placeholderTextColor="#C7C7CC"
          multiline
          numberOfLines={3}
        />

        <TouchableOpacity style={styles.saveButton} onPress={handleSave}>
          <Text style={styles.saveButtonText}>저장</Text>
        </TouchableOpacity>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

function Label({ text }) {
  return <Text style={styles.label}>{text}</Text>;
}

function SelectRow({ options, selected, onSelect }) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.selectRow}>
      {options.map(opt => (
        <TouchableOpacity
          key={opt}
          style={[styles.chip, selected === opt && styles.chipSelected]}
          onPress={() => onSelect(opt)}
        >
          <Text style={[styles.chipText, selected === opt && styles.chipTextSelected]}>
            {opt}
          </Text>
        </TouchableOpacity>
      ))}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F2F2F7' },
  content: { padding: 16, paddingBottom: 50 },
  label: {
    fontSize: 13,
    fontWeight: '600',
    color: '#8E8E93',
    marginBottom: 6,
    marginTop: 16,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  input: {
    backgroundColor: '#fff',
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 15,
    color: '#1C1C1E',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 1,
  },
  textarea: {
    height: 80,
    textAlignVertical: 'top',
  },
  selectRow: {
    flexDirection: 'row',
    marginBottom: 2,
  },
  chip: {
    borderRadius: 20,
    paddingHorizontal: 14,
    paddingVertical: 8,
    marginRight: 8,
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#E5E5EA',
  },
  chipSelected: {
    backgroundColor: '#007AFF',
    borderColor: '#007AFF',
  },
  chipText: {
    fontSize: 14,
    color: '#3C3C43',
  },
  chipTextSelected: {
    color: '#fff',
    fontWeight: '600',
  },
  saveButton: {
    backgroundColor: '#007AFF',
    borderRadius: 14,
    paddingVertical: 16,
    alignItems: 'center',
    marginTop: 30,
  },
  saveButtonText: {
    color: '#fff',
    fontSize: 17,
    fontWeight: '700',
  },
});
