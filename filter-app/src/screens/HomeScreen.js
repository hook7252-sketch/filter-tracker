import React, { useState, useCallback } from 'react';
import {
  View, Text, SectionList, TouchableOpacity,
  StyleSheet, StatusBar, RefreshControl,
} from 'react-native';
import { useFocusEffect } from '@react-navigation/native';
import { loadItems } from '../utils/storage';
import { CATEGORIES } from '../data/initialData';
import { getStatus, statusColor } from '../utils/dateUtils';
import ItemCard from '../components/ItemCard';

export default function HomeScreen({ navigation }) {
  const [sections, setSections] = useState([]);
  const [summary, setSummary] = useState({ overdue: 0, soon: 0, total: 0 });

  useFocusEffect(
    useCallback(() => {
      loadItems().then(items => {
        // Group by category preserving order
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
    }, [])
  );

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

      {/* Summary banner */}
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
          />
        )}
        contentContainerStyle={styles.list}
        staggerAnimation
      />

      <TouchableOpacity
        style={styles.fab}
        onPress={() => navigation.navigate('Edit', { itemId: null })}
        activeOpacity={0.85}
      >
        <Text style={styles.fabIcon}>+</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F2F2F7',
  },
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
  bannerNum: {
    fontSize: 20,
    fontWeight: '700',
  },
  bannerLabel: {
    fontSize: 13,
    color: '#3C3C43',
  },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 20,
    paddingTop: 18,
    paddingBottom: 6,
  },
  sectionTitle: {
    fontSize: 17,
    fontWeight: '700',
    color: '#1C1C1E',
  },
  sectionBadge: {
    marginLeft: 8,
    backgroundColor: '#FF3B3020',
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 2,
  },
  sectionBadgeText: {
    fontSize: 11,
    color: '#FF3B30',
    fontWeight: '600',
  },
  list: {
    paddingBottom: 100,
  },
  fab: {
    position: 'absolute',
    bottom: 30,
    right: 24,
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: '#007AFF',
    justifyContent: 'center',
    alignItems: 'center',
    shadowColor: '#007AFF',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.4,
    shadowRadius: 8,
    elevation: 6,
  },
  fabIcon: {
    fontSize: 28,
    color: '#fff',
    lineHeight: 32,
  },
});
