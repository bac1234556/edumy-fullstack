import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import api from '../api/axiosConfig';

const CategoryContext = createContext(null);

export function CategoryProvider({ children }) {
  const [categories, setCategories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const fetchWithRetry = useCallback(async (signal) => {
    setLoading(true);
    setError('');
    const maxAttempts = 5;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
      try {
        const { data } = await api.get('/categories', { signal });
        const list = Array.isArray(data) ? data : [];
        setCategories(list);
        setError('');
        setLoading(false);
        return list;
      } catch (err) {
        if (signal?.aborted) return;
        
        const status = err.response?.status;
        const isTransient = !status || status >= 500 || status === 408 || status === 429;

        if (!isTransient || attempt === maxAttempts) {
          setCategories([]);
          setError('Không thể tải danh mục.');
          setLoading(false);
          throw err;
        }

        // Exponential backoff delay: 1s, 2s, 4s, 8s
        const backoffMs = 1000 * Math.pow(2, attempt - 1);
        await new Promise((resolve, reject) => {
          const timer = setTimeout(resolve, backoffMs);
          if (signal) {
            const onAbort = () => {
              clearTimeout(timer);
              reject(new Error('Aborted'));
            };
            signal.addEventListener('abort', onAbort, { once: true });
          }
        });
      }
    }
  }, []);

  const refetch = useCallback(() => {
    const controller = new AbortController();
    fetchWithRetry(controller.signal).catch(() => {});
    return controller;
  }, [fetchWithRetry]);

  useEffect(() => {
    const controller = new AbortController();
    fetchWithRetry(controller.signal).catch(() => {});
    return () => controller.abort();
  }, [fetchWithRetry]);

  const findCategory = useCallback((list, id) => {
    for (const category of list) {
      if (Number(category.categoryId) === Number(id)) return category;
      if (Array.isArray(category.subCategories) && category.subCategories.length > 0) {
        const found = findCategory(category.subCategories, id);
        if (found) return found;
      }
    }
    return null;
  }, []);

  const value = useMemo(() => ({
    categories, loading, error, refetch,
    getCategoryById: id => findCategory(categories, id)
  }), [categories, error, findCategory, loading, refetch]);

  return <CategoryContext.Provider value={value}>{children}</CategoryContext.Provider>;
}

export function useCategories() {
  const value = useContext(CategoryContext);
  if (!value) throw new Error('useCategories must be used inside CategoryProvider.');
  return value;
}
