import { Navigate, useLocation } from 'react-router-dom';

import { preserveUserQuery } from './preserveUserQuery';

type UserPreservingNavigateProps = {
  to: string;
  replace?: boolean;
};

export default function UserPreservingNavigate({
  to,
  replace,
}: UserPreservingNavigateProps) {
  const location = useLocation();
  return <Navigate to={preserveUserQuery(to, location.search)} replace={replace} />;
}
