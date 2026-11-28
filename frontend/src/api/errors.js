export function getErrorMessage(error, fallback = 'Something went wrong. Please try again.') {
  if (!error.response) {
    return 'Cannot reach the server. Please check that the API is running.'
  }

  if (error.response.status === 429) {
    return 'Too many attempts. Please wait a minute and try again.'
  }

  const data = error.response.data
  if (data?.message) {
    return data.message
  }

  if (data?.errors) {
    const firstError = Object.values(data.errors).flat()[0]
    if (firstError) {
      return firstError
    }
  }

  return fallback
}