import api from './axiosConfig';

export async function fetchCategories() {
  const { data } = await api.get('/categories');
  return Array.isArray(data) ? data : [];
}
