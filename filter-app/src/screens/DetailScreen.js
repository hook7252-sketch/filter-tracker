import React, { useState, useCallback } from 'react';
import {
  View, Text, ScrollView, TouchableOpacity,
  StyleSheet, Alert, Linking,
} from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { loadItems, saveItems } from '../utils/storage';
import {
  getStatus, getNextDate, formatDate, daysUntil,
  statusColor, statusLabel, todayStr,
} from '../utils/dateUtils';

export default function DetailScreen({ route, navigation }) {
  const { itemId } = route.params;
  const [item, setItem] = useState(null);

  useFocusEffect(
    useCallback(() => {
      loadItems().then(items => {
        const found = items.find(i => i.id === itemId);
        if (found) setItem(found);
      });
    }, [itemId])
  );

  const handleReplaceToday = async () => {
    Alert.alert('교환 완료', '오늘 날짜로 교환 기록을 저장할까요?', [
      { text: '취소', style: 'cancel' },
      {
        text: '저장', onPress: async () => {
          const items = await loadItems();
          const updated = items.map(i =>
            i.id === itemId ? { ...i, lastReplacedDate: todayStr() } : i
          );
          await saveItems(updated);
          const found = updated.find(i => i.id === itemId);
          setItem(found);
        },
      },
    ]);
  };

  const handleDelete = async () => {
    Alert.alert('삭제', `"${item.name}"을(를) 삭제할까요?`, [
      { text: '취소', style: 'cancel' },
      {
        text: '삭제', style: 'destructive', onPress: async () => {
          const items = await loadItems();
          await saveItems(items.filter(i => i.id !== itemId));
          navigation.goBack();
        },
      },
    ]);
  };

  if (!item) return null;

  const status = getStatus(item);
  const nextDate = getNextDate(item);
  const days = nextDate ? daysUntil(nextDate) : null;
  const color = statusColor(status);

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      {/* Status card */}
      <View style={[styles.statusCard, { borderLeftColor: color }]}>
        <View style={styles.statusRow}>
          <View style={[styles.badge, { backgroundColor: color + '22', borderColor: color }]}>
            <Text style={[styles.badgeText, { color }]}>{statusLabel(status)}</Text>
          </View>
          {days !== null && (
            <Text style={[styles.dDay, { color }]}>
              {days >= 0 ? `D-${days}` : `D+${Math.abs(days)}`}
            </Text>
          )}
        </View>
        <Text style={styles.itemName}>{item.name}</Text>
        <Text style={styles.itemLocation}>{item.category} · {item.location}</Text>
      </View>

      {/* Info rows */}
      <View style={styles.section}>
        <InfoRow label="교환주기" value={`${item.replacementCycleDays}일`} />
        <InfoRow label="최근 교환일" value={formatDate(item.lastReplacedDate)} />
        <InfoRow label="다음 교환일" value={formatDate(nextDate)} highlight={status !== 'ok' && status !== 'unknown'} highlightColor={color} />
        <InfoRow label="구입처 가격" value={item.price || '-'} />
        {item.note ? <InfoRow label="메모" value={item.note} /> : null}
      </View>

      {/* Link */}
      {item.link ? (
        <TouchableOpacity
          style={styles.linkButton}
          onPress={() => Linking.openURL(item.link)}
        >
          <Text style={styles.linkButtonText}>구매 링크 열기 →</Text>
        </TouchableOpacity>
      ) : null}

      {/* Actions */}
      <TouchableOpacity style={styles.primaryButton} onPress={handleReplaceToday}>
        <Text style={styles.primaryButtonText}>오늘 교환 완료 ✓</Text>
      </TouchableOpacity>

      <TouchableOpacity
        style={styles.editButton}
        onPress={() => navigation.navigate('Edit', { itemId: item.id })}
      >
        <Text style={styles.editButtonText}>수정</Text>
      </TouchableOpacity>

      <TouchableOpacity style={styles.deleteButton} onPress={handleDelete}>
        <Text style={styles.deleteButtonText}>삭제</Text>
      </TouchableOpacity>
    </ScrollView>
  );
}

function InfoRow({ label, value, highlight, highlightColor }) {
  return (
    <View style={styles.infoRow}>
      <Text style={styles.infoLabel}>{label}</Text>
      <Text style={[styles.infoValue, highlight && { color: highlightColor, fontWeight: '700' }]}>
        {value}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F2F2F7' },
  content: { padding: 16, paddingBottom: 50 },
  statusCard: {
    backgroundColor: '#fff',
    borderRadius: 14,
    padding: 18,
    marginBottom: 16,
    borderLeftWidth: 4,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
    elevation: 2,
  },
  statusRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 10,
    gap: 10,
  },
  badge: {
    borderRadius: 8,
    borderWidth: 1,
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
  badgeText: { fontSize: 13, fontWeight: '600' },
  dDay: { fontSize: 20, fontWeight: '800' },
  itemName: {
    fontSize: 20,
    fontWeight: '700',
    color: '#1C1C1E',
    marginBottom: 4,
  },
  itemLocation: {
    fontSize: 14,
    color: '#8E8E93',
  },
  section: {
    backgroundColor: '#fff',
    borderRadius: 14,
    marginBottom: 16,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.06,
    shadowRadius: 3,
    elevation: 1,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 13,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#E5E5EA',
  },
  infoLabel: { fontSize: 15, color: '#3C3C43' },
  infoValue: { fontSize: 15, color: '#1C1C1E', maxWidth: '60%', textAlign: 'right' },
  linkButton: {
    backgroundColor: '#007AFF15',
    borderRadius: 12,
    paddingVertical: 13,
    alignItems: 'center',
    marginBottom: 12,
    borderWidth: 1,
    borderColor: '#007AFF40',
  },
  linkButtonText: { color: '#007AFF', fontSize: 15, fontWeight: '600' },
  primaryButton: {
    backgroundColor: '#34C759',
    borderRadius: 14,
    paddingVertical: 15,
    alignItems: 'center',
    marginBottom: 10,
  },
  primaryButtonText: { color: '#fff', fontSize: 16, fontWeight: '700' },
  editButton: {
    backgroundColor: '#007AFF',
    borderRadius: 14,
    paddingVertical: 15,
    alignItems: 'center',
    marginBottom: 10,
  },
  editButtonText: { color: '#fff', fontSize: 16, fontWeight: '600' },
  deleteButton: {
    backgroundColor: '#FF3B3015',
    borderRadius: 14,
    paddingVertical: 14,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#FF3B3040',
  },
  deleteButtonText: { color: '#FF3B30', fontSize: 15, fontWeight: '600' },
});
