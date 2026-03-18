import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { getStatus, getNextDate, formatDate, daysUntil, statusColor, statusLabel } from '../utils/dateUtils';

export default function ItemCard({ item, onPress }) {
  const status = getStatus(item);
  const nextDate = getNextDate(item);
  const days = nextDate ? daysUntil(nextDate) : null;
  const color = statusColor(status);

  return (
    <TouchableOpacity style={styles.card} onPress={onPress} activeOpacity={0.7}>
      <View style={styles.left}>
        <View style={[styles.statusDot, { backgroundColor: color }]} />
      </View>
      <View style={styles.body}>
        <Text style={styles.name} numberOfLines={1}>{item.name}</Text>
        <Text style={styles.location}>{item.location}</Text>
        <Text style={styles.cycle}>교환주기: {item.replacementCycleDays}일</Text>
      </View>
      <View style={styles.right}>
        <View style={[styles.badge, { backgroundColor: color + '22', borderColor: color }]}>
          <Text style={[styles.badgeText, { color }]}>{statusLabel(status)}</Text>
        </View>
        {nextDate ? (
          <Text style={styles.nextDate}>
            {days >= 0 ? `D-${days}` : `D+${Math.abs(days)}`}
          </Text>
        ) : (
          <Text style={styles.noDate}>날짜 미입력</Text>
        )}
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#fff',
    borderRadius: 12,
    marginHorizontal: 16,
    marginVertical: 5,
    padding: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.08,
    shadowRadius: 4,
    elevation: 2,
  },
  left: {
    marginRight: 12,
  },
  statusDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  body: {
    flex: 1,
  },
  name: {
    fontSize: 15,
    fontWeight: '600',
    color: '#1C1C1E',
    marginBottom: 2,
  },
  location: {
    fontSize: 12,
    color: '#8E8E93',
    marginBottom: 2,
  },
  cycle: {
    fontSize: 12,
    color: '#C7C7CC',
  },
  right: {
    alignItems: 'flex-end',
    minWidth: 72,
  },
  badge: {
    borderRadius: 8,
    borderWidth: 1,
    paddingHorizontal: 8,
    paddingVertical: 3,
    marginBottom: 4,
  },
  badgeText: {
    fontSize: 11,
    fontWeight: '600',
  },
  nextDate: {
    fontSize: 13,
    fontWeight: '700',
    color: '#1C1C1E',
  },
  noDate: {
    fontSize: 11,
    color: '#C7C7CC',
  },
});
