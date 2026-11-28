import { Typography } from '@mui/material'

export default function PlaceholderPage({ title }) {
  return (
    <Typography variant="h5" component="h1">
      {title}
    </Typography>
  )
}