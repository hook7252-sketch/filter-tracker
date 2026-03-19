import React, { useState, useCallback } from 'react';
import {
  View, Text, SectionList, TouchableOpacity,
  StyleSheet, StatusBar, Alert,
} from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { loadItems, saveItems } from '../utils/storage';
import { CATEGORIES } from '../data/initialData';
import { getStatus, statusColor } from '../utils/dateUtils';
import ItemCard from '../components/ItemCard';

export default function HomeScreen({ navigation }) {
  const [sections, setSections] = useState([]);
  const [summary, setSummary] = useState({ overdue: 0, soon: 0, total: 0 });

  const refresh = useCallback(() => {
    loadItems().then(items => {
      const grouped = {};
      CATEGORIES.forEach(cat => { grouped[cat] = []; });
      items.forEach(item => {
        const cat = item.category || '기타';
        if (!grouped[cat]) grouped[cat] = [];
        grouped[cat].push(item);
      });
      const secs = Object.entries(grouped)
        .filter(([, data]) => data.length > 0)
        .map(([title, data]) => ({ title, data }));
      setSections(secs);

      const overdue = items.filter(i => getStatus(i) === 'overdue').length;
      const soon = items.filter(i => getStatus(i) === 'soon').length;
      setSummary({ overdue, soon, total: items.length });
    });
  }, []);

  useFocusEffect(refresh);

  const handleLongPress = (item) => {
    Alert.alert(item.name, '어떤 작업을 하시겠어요?', [
      {
        text: '✏️  수정',
        onPress: () => navigation.navigate('Edit', { itemId: item.id }),
      },
      {
        text: '🗑️  삭제',
        style: 'destructive',
        onPress: () => confirmDelete(item),
      },
      { text: '취소', style: 'cancel' },
    ]);
  };

  const confirmDelete = (item) => {
    Alert.alert('삭제 확인', `"${item.name}"을(를) 삭제할까요?`, [
      { text: '취소', style: 'cancel' },
      {
        text: '삭제',
        style: 'destructive',
        onPress: async () => {
          const items = await loadItems();
          await saveItems(items.filter(i => i.id !== item.id));
          refresh();
        },
      },
    ]);
  };

  const renderSectionHeader = ({ section: { title, data } }) => {
    const overdueCount = data.filter(i => getStatus(i) === 'overdue').length;
    return (
      <View style={styles.sectionHeader}>
        <Text style={styles.sectionTitle}>{title}</Text>
        {overdueCount > 0 && (
          <View style={styles.sectionBadge}>
            <Text style={styles.sectionBadgeText}>{overdueCount}개 교체 필요</Text>
          </View>
        )}
      </View>
    );
  };

  return (
    <View style={styles.container}>
      <StatusBar barStyle="dark-content" backgroundColor="#F2F2F7" />

      {(summary.overdue > 0 || summary.soon > 0) && (
        <View style={styles.banner}>
          {summary.overdue > 0 && (
            <View style={[styles.bannerItem, { backgroundColor: '#FF3B3015' }]}>
              <Text style={[styles.bannerNum, { color: '#FF3B30' }]}>{summary.overdue}</Text>
              <Text style={styles.bannerLabel}>교체 필요</Text>
            </View>
          )}
          {summary.soon > 0 && (
            <View style={[styles.bannerItem, { backgroundColor: '#FF950015' }]}>
              <Text style={[styles.bannerNum, { color: '#FF9500' }]}>{summary.soon}</Text>
              <Text style={styles.bannerLabel}>교체 임박</Text>
            </View>
          )}
        </View>
      )}

      <SectionList
        sections={sections}
        keyExtractor={item => item.id}
        renderSectionHeader={renderSectionHeader}
        renderItem={({ item }) => (
          <ItemCard
            item={item}
            onPress={() => navigation.navigate('Detail', { itemId: item.id })}
            onLongPress={() => handleLongPress(item)}
          />
        )}
        contentContainerStyle={styles.list}
        ListEmptyComponent={
          <View style={styles.empty}>
            <Text style={styles.emptyText}>소모품이 없습니다.</Text>
            <Text style={styles.emptyHint}>아래 + 버튼으로 추가해보세요.</Text>
          </View>
        }
      />

      {/* FAB */}
      <TouchableOpacity
        style={styles.fab}
        onPress={() => navigation.navigate('Edit', { itemId: null })}
        activeOpacity={0.85}
      >
        <Text style={styles.fabIcon}>＋</Text>
        <Text style={styles.fabLabel}>제품 추가</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F2F2F7' },
  banner: {
    flexDirection: 'row',
    paddingHorizontal: 16,
    paddingVertical: 10,
    gap: 10,
  },
  bannerItem: {
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 8,
    gap: 6,
  },
  bannerNum: { fontSize: 20, fontWeight: '700' },
  bannerLabel: { fontSize: 13, color: '#3C3C43' },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 20,
    paddingTop: 18,
    paddingBottom: 6,
  },
  sectionTitle: { fontSize: 17, fontWeight: '700', color: '#1C1C1E' },
  sectionBadge: {
    marginLeft: 8,
    backgroundColor: '#FF3B3020',
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 2,
  },
  sectionBadgeText: { fontSize: 11, color: '#FF3B30', fontWeight: '600' },
  list: { paddingBottom: 110 },
  empty: {
    alignItems: 'center',
    marginTop: 80,
  },
  emptyText: { fontSize: 17, fontWeight: '600', color: '#8E8E93' },
  emptyHint: { fontSize: 13, color: '#C7C7CC', marginTop: 6 },
  fab: {
    position: 'absolute',
    bottom: 30,
    right: 20,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#007AFF',
    borderRadius: 28,
    paddingVertical: 14,
    paddingHorizontal: 20,
    gap: 6,
    shadowColor: '#007AFF',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.4,
    shadowRadius: 8,
    elevation: 6,
  },
  fabIcon: { fontSize: 20, color: '#fff', lineHeight: 24 },
  fabLabel: { fontSize: 15, color: '#fff', fontWeight: '700' },
});
