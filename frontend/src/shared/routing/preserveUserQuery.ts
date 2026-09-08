export function preserveUserQuery(to: string, currentSearch: string) {
  if (!import.meta.env.DEV) {
    return to;
  }

  const user = new URLSearchParams(currentSearch).get('user')?.trim();

  if (!user) {
    return to;
  }

  const hashIndex = to.indexOf('#');
  const hash = hashIndex >= 0 ? to.slice(hashIndex) : '';
  const pathAndSearch = hashIndex >= 0 ? to.slice(0, hashIndex) : to;
  const queryIndex = pathAndSearch.indexOf('?');
  const pathname = queryIndex >= 0 ? pathAndSearch.slice(0, queryIndex) : pathAndSearch;
  const search = queryIndex >= 0 ? pathAndSearch.slice(queryIndex + 1) : '';
  const params = new URLSearchParams(search);

  if (!params.has('user')) {
    params.set('user', user);
  }

  return `${pathname}?${params.toString()}${hash}`;
}
