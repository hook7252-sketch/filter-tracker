import React from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import HomeScreen from './src/screens/HomeScreen';
import DetailScreen from './src/screens/DetailScreen';
import EditScreen from './src/screens/EditScreen';

const Stack = createNativeStackNavigator();

export default function App() {
  return (
    <NavigationContainer>
      <Stack.Navigator
        initialRouteName="Home"
        screenOptions={{
          headerStyle: { backgroundColor: '#F2F2F7' },
          headerShadowVisible: false,
          headerTitleStyle: { fontWeight: '700', fontSize: 17 },
          headerBackTitle: '뒤로',
        }}
      >
        <Stack.Screen
          name="Home"
          component={HomeScreen}
          options={{ title: '소모품 교환 관리' }}
        />
        <Stack.Screen
          name="Detail"
          component={DetailScreen}
          options={{ title: '상세 정보' }}
        />
        <Stack.Screen
          name="Edit"
          component={EditScreen}
          options={{ title: '편집' }}
        />
      </Stack.Navigator>
    </NavigationContainer>
  );
}
