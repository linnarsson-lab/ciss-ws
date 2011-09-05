<?php
defined('_JEXEC') or die;
jimport('joomla.form.helper');
JFormHelper::loadFieldClass('list');

class JFormFieldProject extends JFormFieldList {

	protected $type = 'Project';

	protected function getOptions() {
		$db = JFactory::getDBO();
		$query = new JDatabaseQuery;
		$query->select('id, title, plateid, platereference, species, tissue, sampletype, collectionmethod, 
                 weightconcentration, fragmentlength, molarconcentration, description, 
                protocol, barcodeset, labbookpage, comment, user, time');
		$query->from('#__aaaproject');
		$db->setQuery((string)$query);
		$messages = $db->loadObjectList();

		$options = array();
		if ($messages) {
			foreach($messages as $message) {
				$options[] = JHtml::_('select.option', $message->id, $message->title, $message->plateid, $message->platereference, $message->species, $message->tissue, $message->sampletype, 
        $message->collectionmethod, $message->weightconcentration, $message->fragmentlength, $message->molarconcentration, $message->description, $message->protocol, $message->barcodeset, $message->labbookpage, $message->comment, $message->user, $message->time);
			}
		}
		$options = array_merge(parent::getOptions(), $options);
		return $options;
	}
}
