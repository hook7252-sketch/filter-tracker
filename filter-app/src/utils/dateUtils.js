export function addDays(dateStr, days) {
  if (!dateStr) return null;
  const date = new Date(dateStr);
  date.setDate(date.getDate() + days);
  return date.toISOString().split('T')[0];
}

export function formatDate(dateStr) {
  if (!dateStr) return '-';
  const [year, month, day] = dateStr.split('-');
  return `${year}.${month}.${day}`;
}

export function todayStr() {
  return new Date().toISOString().split('T')[0];
}

export function daysUntil(dateStr) {
  if (!dateStr) return null;
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const target = new Date(dateStr);
  const diff = Math.round((target - today) / (1000 * 60 * 60 * 24));
  return diff;
}

// Returns 'overdue' | 'soon' | 'ok' | 'unknown'
export function getStatus(item) {
  const nextDate = getNextDate(item);
  if (!nextDate) return 'unknown';
  const days = daysUntil(nextDate);
  if (days < 0) return 'overdue';
  if (days <= 14) return 'soon';
  return 'ok';
}

export function getNextDate(item) {
  if (!item.lastReplacedDate) return null;
  return addDays(item.lastReplacedDate, item.replacementCycleDays);
}

export function statusLabel(status) {
  switch (status) {
    case 'overdue': return '교체 필요';
    case 'soon': return '교체 임박';
    case 'ok': return '정상';
    default: return '미입력';
  }
}

export function statusColor(status) {
  switch (status) {
    case 'overdue': return '#FF3B30';
    case 'soon': return '#FF9500';
    case 'ok': return '#34C759';
    default: return '#8E8E93';
  }
}
