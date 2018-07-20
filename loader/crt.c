void *memcpy(void *dst, const void *src, int n) {
	char *d = dst;
	const char *s = src;
	while (n--)
		*d++ = *s++;
	return dst;
}

void *memset(void *dst, char c, int n) {
	char *d = dst;
	while (n--)
		*d = c;
	return dst;
}
