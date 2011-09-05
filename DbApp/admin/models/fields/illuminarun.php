<?php
defined('_JEXEC') or die;
jimport('joomla.form.helper');
JFormHelper::loadFieldClass('list');

class JFormFieldIlluminaRun extends JFormFieldList {

	protected $type = 'IlluminaRun';

	protected function getOptions() {
		$db = JFactory::getDBO();
		$query = new JDatabaseQuery;
		$query->select('id, illuminarunid, rundate, comment, user, time');
		$query->from('#__aaailluminarun');
		$db->setQuery((string)$query);
		$messages = $db->loadObjectList();

		$options = array();
		if ($messages) {
			foreach($messages as $message) {
				$options[] = JHtml::_('select.option', $message->id, $message->illuminarunid, $message->rundate, $message->comment, $message->user, $message->time);
			}
		}
		$options = array_merge(parent::getOptions(), $options);
		return $options;
	}
}

