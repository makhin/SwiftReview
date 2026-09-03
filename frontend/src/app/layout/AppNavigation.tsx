import List from 'devextreme-react/list';
import { useLocation, useNavigate } from 'react-router-dom';

type NavigationItem = {
  path: string;
  text: string;
  icon: string;
};

const navigationItems: NavigationItem[] = [
  { path: '/messages', text: 'Messages', icon: 'email' },
  { path: '/me', text: 'Current user', icon: 'user' },
];

type AppNavigationProps = {
  onNavigate: () => void;
};

export default function AppNavigation({ onNavigate }: AppNavigationProps) {
  const location = useLocation();
  const navigate = useNavigate();

  return (
    <aside className="app-sidebar smbc-sidebar" id="application-navigation">
      <nav aria-label="Application navigation">
        <List
          items={navigationItems}
          keyExpr="path"
          displayExpr="text"
          selectionMode="single"
          selectedItemKeys={[location.pathname]}
          focusStateEnabled
          activeStateEnabled
          onItemClick={({ itemData }) => {
            const item = itemData as NavigationItem;
            void navigate(item.path);
            onNavigate();
          }}
          elementAttr={{ 'aria-label': 'Application pages' }}
        />
      </nav>
    </aside>
  );
}
